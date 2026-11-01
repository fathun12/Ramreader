using System.Globalization;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;

namespace Ramreader.Core.Scanning;

public enum SignatureState
{
    Found,        // alamat nilai berhasil dihitung
    Searching,    // pemindaian modul sedang berjalan di latar (sekali saja per proses)
    NotFound,     // pola tidak ada di game ini
    Ambiguous,    // pola cocok di lebih dari satu tempat
    CodeOnly,     // signature lokasi kode (tanpa informasi RIP)
    Unreadable    // kode ditemukan tapi alamat yang dihitung tidak terbaca
}

public sealed record SignatureResolution(SignatureState State, ulong? Address, ulong? CodeAddress);

/// <summary>
/// Mengubah SignatureRef jadi alamat nilai di game yang sedang berjalan:
/// cari pola di modul utama -> baca disp32 -> alamat RIP -> + StaticDelta -> ikuti Offsets.
/// Lokasi pola di-cache per proses supaya pemanggilan berulang (timer 1 detik) murah.
/// </summary>
public static class SignatureResolver
{
    private sealed class Entry
    {
        public ulong? Match;
        public bool Ambiguous;
        public bool Scanning;
        public DateTime LastScan;
    }

    private static readonly Dictionary<string, Entry> Cache = new();
    private static readonly object Gate = new();
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    /// <summary>Tidak memblokir: pemindaian pertama berjalan di latar, sementara itu hasilnya Searching.</summary>
    public static SignatureResolution Resolve(ProcessMemory memory, SignatureRef sig)
    {
        var module = memory.MainModule;
        if (module is null || !AobPattern.TryParse(sig.Pattern, out var pattern, out _))
            return new SignatureResolution(SignatureState.NotFound, null, null);

        string key = memory.ProcessId + "|" + pattern;
        Entry entry;
        lock (Gate)
        {
            if (!Cache.TryGetValue(key, out entry!))
            {
                entry = new Entry();
                Cache[key] = entry;
            }
        }

        ulong? match;
        lock (Gate) match = entry.Match;

        // Lokasi yang tersimpan harus masih cocok (kode bisa di-unload / diubah).
        if (match is { } known && !StillMatches(memory, pattern, known))
        {
            lock (Gate)
            {
                entry.Match = null;
                entry.LastScan = default;
            }
            match = null;
        }

        if (match is null)
        {
            bool startScan = false;
            lock (Gate)
            {
                if (!entry.Scanning && (entry.LastScan == default || DateTime.UtcNow - entry.LastScan > RetryAfter))
                {
                    entry.Scanning = true;
                    startScan = true;
                }
            }

            if (startScan) _ = Task.Run(() => ScanInto(memory, module, pattern, entry));

            lock (Gate)
            {
                if (entry.Scanning) return new SignatureResolution(SignatureState.Searching, null, null);
                if (entry.Ambiguous) return new SignatureResolution(SignatureState.Ambiguous, null, null);
                if (entry.Match is null) return new SignatureResolution(SignatureState.NotFound, null, null);
                match = entry.Match;
            }
        }

        ulong matchAddress = match.Value;
        if (sig.DispOffset < 0)
            return new SignatureResolution(SignatureState.CodeOnly, null, matchAddress);

        var address = ResolveAt(memory, sig, matchAddress);
        return address is null
            ? new SignatureResolution(SignatureState.Unreadable, null, matchAddress)
            : new SignatureResolution(SignatureState.Found, address, matchAddress);
    }

    /// <summary>Hitung alamat nilai dari lokasi pola yang sudah diketahui. Null kalau ada yang tidak terbaca.</summary>
    public static ulong? ResolveAt(ProcessMemory memory, SignatureRef sig, ulong matchAddress)
    {
        if (sig.DispOffset < 0) return null;
        if (!memory.TryRead<int>(matchAddress + (ulong)sig.DispOffset, out int disp)) return null;

        long ripTarget = (long)matchAddress + sig.InstructionEnd + disp;
        ulong current = (ulong)ripTarget + sig.StaticDelta;

        var offsets = ParseOffsets(sig.Offsets);
        if (offsets is null) return null;

        // Sama seperti PointerChain: baca pointer di alamat sekarang, tambah offset, ulangi.
        foreach (ulong offset in offsets)
        {
            var pointer = memory.ReadPointer(current);
            if (pointer is null) return null;
            current = pointer.Value + offset;
        }

        return current;
    }

    private static List<ulong>? ParseOffsets(string text)
    {
        var list = new List<ulong>();
        foreach (var piece in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ulong.TryParse(piece, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                return null;
            list.Add(value);
        }
        return list;
    }

    private static bool StillMatches(ProcessMemory memory, AobPattern pattern, ulong address)
    {
        var bytes = memory.Read(address, pattern.Length);
        return bytes is not null && pattern.MatchesAt(bytes, 0);
    }

    private static void ScanInto(ProcessMemory memory, ModuleInfo module, AobPattern pattern, Entry entry)
    {
        try
        {
            var hits = AobScanner.Scan(memory, module, pattern, 2, null, CancellationToken.None);
            lock (Gate)
            {
                entry.Ambiguous = hits.Count > 1;
                entry.Match = hits.Count == 1 ? hits[0] : null;
            }
        }
        catch
        {
            // Game ditutup di tengah pemindaian: anggap tidak ditemukan.
        }
        finally
        {
            lock (Gate)
            {
                entry.Scanning = false;
                entry.LastScan = DateTime.UtcNow;
            }
        }
    }
}