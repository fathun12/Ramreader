using Ramreader.Core.Memory;
using Ramreader.Core.Models;

namespace Ramreader.Core.Scanning;

/// <summary>
/// Scanner nilai bergaya Cheat Engine: scan pertama (nilai tepat) lalu scan lanjutan
/// untuk menyaring kandidat. Dijalankan dari thread latar.
/// </summary>
public sealed class ValueScanner
{
    /// <summary>Batas jumlah kandidat supaya RAM aplikasi ini tidak membengkak.</summary>
    public const int MaxHits = 5_000_000;

    private const ulong ReadWindow = 1UL << 20; // 1 MB per pembacaan batch saat scan lanjutan

    private List<ScanHit> _hits = new();

    public ScanValueType ValueType { get; private set; } = ScanValueType.Int32;
    public bool HasScan { get; private set; }

    /// <summary>True kalau hasil dipotong karena melewati MaxHits (alamat target mungkin tidak ikut).</summary>
    public bool Truncated { get; private set; }

    public IReadOnlyList<ScanHit> Hits => _hits;
    public int Count => _hits.Count;

    public void Reset()
    {
        _hits = new List<ScanHit>();
        HasScan = false;
        Truncated = false;
    }

    /// <summary>Scan pertama: cari semua alamat writable yang berisi nilai persis sama.</summary>
    public void FirstScan(ProcessMemory memory, ScanValueType type, ulong target,
        IProgress<double>? progress, CancellationToken ct)
    {
        int size = ValueCodec.SizeOf(type);
        var hits = new List<ScanHit>();
        bool truncated = false;

        double total = memory.EnumerateRegions(true).Sum(r => (double)r.Size);
        if (total <= 0) total = 1;
        double done = 0;

        foreach (var (address, data) in memory.ReadChunks(true))
        {
            ct.ThrowIfCancellationRequested();

            for (int i = 0; i + size <= data.Length; i += size)
            {
                ulong raw = size == 4 ? BitConverter.ToUInt32(data, i) : BitConverter.ToUInt64(data, i);
                if (raw != target) continue;

                if (hits.Count >= MaxHits)
                {
                    truncated = true;
                    break;
                }
                hits.Add(new ScanHit(address + (ulong)i, raw, raw));
            }

            done += data.Length;
            progress?.Report(done / total);
            if (truncated) break;
        }

        _hits = hits;
        ValueType = type;
        HasScan = true;
        Truncated = truncated;
        progress?.Report(1);
    }

    /// <summary>Scan lanjutan: baca ulang tiap kandidat, simpan yang memenuhi mode.</summary>
    public void NextScan(ProcessMemory memory, ScanMode mode, ulong target,
        IProgress<double>? progress, CancellationToken ct)
    {
        if (!HasScan) throw new InvalidOperationException("Belum ada scan pertama.");

        int size = ValueCodec.SizeOf(ValueType);
        var survivors = new List<ScanHit>();
        int n = _hits.Count;
        int i = 0;

        while (i < n)
        {
            ct.ThrowIfCancellationRequested();

            // Kandidat berurutan menurut alamat, jadi dibaca per blok supaya tidak
            // memanggil ReadProcessMemory jutaan kali.
            ulong start = _hits[i].Address;
            int j = i + 1;
            while (j < n && _hits[j].Address + (ulong)size - start <= ReadWindow) j++;

            int length = (int)(_hits[j - 1].Address + (ulong)size - start);
            byte[]? block = memory.Read(start, length);

            for (int k = i; k < j; k++)
            {
                var hit = _hits[k];
                ulong current;

                if (block is not null)
                {
                    current = ValueCodec.ReadRaw(block, (int)(hit.Address - start), size);
                }
                else
                {
                    // Ada halaman yang tidak terbaca di dalam blok: coba satu per satu.
                    var single = memory.Read(hit.Address, size);
                    if (single is null) continue;
                    current = ValueCodec.ReadRaw(single, 0, size);
                }

                if (Matches(mode, hit.Value, current, target))
                    survivors.Add(new ScanHit(hit.Address, current, hit.Value));
            }

            i = j;
            progress?.Report((double)i / n);
        }

        _hits = survivors;
        progress?.Report(1);
    }

    /// <summary>Baca nilai terkini satu alamat (untuk tampilan langsung di daftar).</summary>
    public ulong? ReadCurrent(ProcessMemory memory, ulong address)
    {
        int size = ValueCodec.SizeOf(ValueType);
        var bytes = memory.Read(address, size);
        return bytes is null ? null : ValueCodec.ReadRaw(bytes, 0, size);
    }

    private bool Matches(ScanMode mode, ulong old, ulong current, ulong target) => mode switch
    {
        ScanMode.Exact => current == target,
        ScanMode.Changed => current != old,
        ScanMode.Unchanged => current == old,
        ScanMode.Increased => ValueCodec.Compare(ValueType, current, old) > 0,
        ScanMode.Decreased => ValueCodec.Compare(ValueType, current, old) < 0,
        _ => false
    };
}