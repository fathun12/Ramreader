using Ramreader.Core.Memory;
using Ramreader.Core.Models;

namespace Ramreader.Core.Scanning;

/// <summary>Hasil pembuatan signature. Pattern null = gagal (lihat Message).</summary>
public sealed record SignatureResult(AobPattern? Pattern, int MatchCount, int DispOffset, int InstructionEnd, string Message);

public static class SignatureMaker
{
    private static readonly int[] ExactLengths = { 8, 12, 16, 20, 24, 32, 40, 48 };
    private static readonly int[] Before = { 3, 6, 10 };
    private static readonly int[] After = { 4, 8, 12, 16, 24, 32 };

    /// <summary>
    /// SPEC 7.3: dari alamat instruksi, coba panjang 8..48 byte persis dan ambil yang pertama unik di modul.
    /// Hasilnya hanya menemukan lokasi kode (tidak ada informasi RIP).
    /// </summary>
    public static SignatureResult MakeExact(ProcessMemory memory, ModuleInfo module, ulong instructionAddress,
        IProgress<double>? progress, CancellationToken ct)
    {
        ulong moduleEnd = module.BaseAddress + module.Size;
        if (instructionAddress < module.BaseAddress || instructionAddress >= moduleEnd)
            return Fail("Alamat ini berada di luar modul utama game, jadi tidak bisa dibuat signature.");

        AobPattern? last = null;
        int lastCount = 0;

        for (int k = 0; k < ExactLengths.Length; k++)
        {
            ct.ThrowIfCancellationRequested();

            int length = ExactLengths[k];
            if (instructionAddress + (ulong)length > moduleEnd) break;

            var bytes = memory.Read(instructionAddress, length);
            if (bytes is null) return Fail("Gagal membaca kode di alamat itu. Cek alamatnya.");

            var pattern = AobPattern.Create(bytes, Array.Empty<int>());
            if (pattern is null) continue;

            int count = AobScanner.Scan(memory, module, pattern, 2, null, ct).Count;
            progress?.Report((k + 1.0) / ExactLengths.Length);

            last = pattern;
            lastCount = count;
            if (count == 1)
                return new SignatureResult(pattern, 1, -1, 0,
                    $"Pola unik ({length} byte). Pola ini hanya menemukan lokasi kode. " +
                    "Ganti byte yang bisa berubah antar versi (mis. alamat 4 byte) dengan ??, lalu Uji Pola.");
        }

        if (last is null) return Fail("Tidak bisa membuat pola dari alamat itu.");

        return new SignatureResult(last, lastCount, -1, 0,
            $"Belum unik sampai {last.Length} byte (cocok di {lastCount} tempat). Coba alamat instruksi lain.");
    }

    /// <summary>
    /// Dari satu rujukan RIP-relative: ambil kode di sekitar disp32, jadikan 4 byte disp32 sebagai ??,
    /// dan pilih jendela terpendek yang unik di modul.
    /// </summary>
    public static SignatureResult MakeForReference(ProcessMemory memory, ModuleInfo module, RipReference reference,
        IProgress<double>? progress, CancellationToken ct)
    {
        ulong moduleStart = module.BaseAddress;
        ulong moduleEnd = moduleStart + module.Size;

        var windows = new List<(int Pre, int Post)>();
        foreach (int pre in Before)
            foreach (int post in After)
                windows.Add((pre, post));
        windows.Sort((a, b) => (a.Pre + a.Post).CompareTo(b.Pre + b.Post));

        SignatureResult? best = null;

        for (int k = 0; k < windows.Count; k++)
        {
            ct.ThrowIfCancellationRequested();

            var (pre, post) = windows[k];
            ulong start = reference.DispAddress - (ulong)pre;
            ulong end = reference.DispAddress + 4 + (ulong)post;
            if (start < moduleStart || end > moduleEnd) continue;

            var bytes = memory.Read(start, pre + 4 + post);
            if (bytes is null) continue;

            var pattern = AobPattern.Create(bytes, new[] { pre, pre + 1, pre + 2, pre + 3 });
            if (pattern is null) continue;

            int count = AobScanner.Scan(memory, module, pattern, 2, null, ct).Count;
            progress?.Report((k + 1.0) / windows.Count);

            int instructionEnd = pre + 4 + reference.ImmSize;
            if (count == 1)
                return new SignatureResult(pattern, 1, pre, instructionEnd,
                    $"Pola unik ({pattern.Length} byte, 4 byte alamat sudah jadi ??). " +
                    "Klik Uji Pola lalu Simpan ke Alamat.");

            best = new SignatureResult(pattern, count, pre, instructionEnd,
                $"Belum unik sampai {pattern.Length} byte (cocok di {count} tempat). Pilih baris kandidat lain.");
        }

        return best ?? Fail("Tidak bisa membaca kode di sekitar baris ini. Pilih baris kandidat lain.");
    }

    private static SignatureResult Fail(string message) => new(null, 0, -1, 0, message);
}