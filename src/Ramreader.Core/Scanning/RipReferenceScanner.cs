using Ramreader.Core.Memory;
using Ramreader.Core.Models;

namespace Ramreader.Core.Scanning;

/// <summary>Satu tempat di kode game yang memakai alamat statis lewat operand RIP-relative.</summary>
/// <param name="DispAddress">Alamat 4 byte displacement (disp32) di dalam instruksi.</param>
/// <param name="ImmSize">Perkiraan ukuran immediate setelah disp32 (0, 1, atau 4 byte).</param>
/// <param name="Delta">Alamat statis jalur dikurangi alamat yang dirujuk kode (0 = persis alamat itu).</param>
public sealed record RipReference(ulong DispAddress, int ImmSize, ulong Delta);

/// <summary>
/// Mencari instruksi x64 beroperand [rip+disp32] yang menunjuk ke alamat statis tertentu,
/// tanpa disassembler: byte sebelum disp32 harus ModRM dengan mod=00 rm=101, dan
/// DispAddress + 4 + immediate + disp32 harus jatuh di [static - maxDelta, static].
/// </summary>
public static class RipReferenceScanner
{
    private const int ChunkSize = 16 * 1024 * 1024;
    private const int Cap = 20_000;

    public static List<RipReference> Find(ProcessMemory memory, ModuleInfo module, ulong staticAddress,
        ulong maxDelta, int maxResults, IProgress<double>? progress, CancellationToken ct)
    {
        var results = new List<RipReference>();
        long high = (long)staticAddress;
        long low = (long)(staticAddress >= maxDelta ? staticAddress - maxDelta : 0UL);

        var ranges = AobScanner.ModuleRanges(memory, module, true);
        double total = ranges.Sum(r => (double)(r.Hi - r.Lo));
        if (total <= 0) total = 1;
        double done = 0;
        bool full = false;

        foreach (var (lo, hi) in ranges)
        {
            ulong length = hi - lo;
            for (ulong offset = 0; offset < length && !full; offset += ChunkSize)
            {
                ct.ThrowIfCancellationRequested();

                ulong remaining = length - offset;
                int count = (int)Math.Min(remaining, (ulong)ChunkSize);      // posisi disp32 yang diperiksa
                int lead = offset > 0 ? 1 : 0;                                // 1 byte konteks sebelum chunk (ModRM)
                int take = (int)Math.Min(remaining + (ulong)lead, (ulong)(ChunkSize + lead + 3));
                ulong readStart = lo + offset - (ulong)lead;

                var data = memory.Read(readStart, take);
                done += count;
                progress?.Report(done / total);
                if (data is null) continue;

                int first = Math.Max(lead, 1);
                int last = Math.Min(lead + count - 1, data.Length - 4);

                for (int i = first; i <= last; i++)
                {
                    if ((data[i - 1] & 0xC7) != 0x05) continue;

                    int disp = BitConverter.ToInt32(data, i);
                    int imm = ImmediateSize(data, i);
                    long dispAddress = (long)(readStart + (ulong)i);
                    long target = dispAddress + 4 + imm + disp;

                    if (target < low || target > high) continue;

                    results.Add(new RipReference((ulong)dispAddress, imm, (ulong)(high - target)));
                    if (results.Count >= Cap) { full = true; break; }
                }
            }

            if (full) break;
        }

        results.Sort((a, b) => a.Delta != b.Delta
            ? a.Delta.CompareTo(b.Delta)
            : a.DispAddress.CompareTo(b.DispAddress));
        if (results.Count > maxResults)
            results.RemoveRange(maxResults, results.Count - maxResults);

        progress?.Report(1);
        return results;
    }

    /// <summary>
    /// Tebakan ukuran immediate setelah disp32 dari opcode di depan ModRM. Hanya perkiraan:
    /// kalau salah, alamat yang dihitung salah dan Simpan menolaknya (dicocokkan dengan jalur utama).
    /// </summary>
    private static int ImmediateSize(byte[] data, int i)
    {
        if (i < 2) return 0;

        switch (data[i - 2])
        {
            case 0x80: case 0x83: case 0xC6: case 0x6B:
                return 1;
            case 0x81: case 0xC7: case 0x69:
                return 4;
            case 0xF6:
                return ((data[i - 1] >> 3) & 7) <= 1 ? 1 : 0;   // test r/m8, imm8
            case 0xF7:
                return ((data[i - 1] >> 3) & 7) <= 1 ? 4 : 0;   // test r/m, imm32
            case 0xBA: case 0x70: case 0x71: case 0x72: case 0x73:
                return i >= 3 && data[i - 3] == 0x0F ? 1 : 0;
        }

        return 0;
    }
}