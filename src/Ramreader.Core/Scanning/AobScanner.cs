using Ramreader.Core.Memory;
using Ramreader.Core.Models;

namespace Ramreader.Core.Scanning;

/// <summary>Pencari pola byte (AOB) di dalam satu modul game.</summary>
public static class AobScanner
{
    private const int ChunkSize = 16 * 1024 * 1024;

    /// <summary>
    /// Cari pola di seluruh region terbaca milik modul. Chunk 16 MB dengan overlap (panjang pola - 1)
    /// dan hanya posisi awal di dalam chunk yang dihitung, jadi tidak ada hasil ganda.
    /// </summary>
    public static List<ulong> Scan(ProcessMemory memory, ModuleInfo module, AobPattern pattern,
        int maxResults, IProgress<double>? progress, CancellationToken ct)
    {
        var results = new List<ulong>();
        var ranges = ModuleRanges(memory, module, false);

        double total = ranges.Sum(r => (double)(r.Hi - r.Lo));
        if (total <= 0) total = 1;
        double done = 0;
        int overlap = pattern.Length - 1;

        foreach (var (lo, hi) in ranges)
        {
            ulong length = hi - lo;
            for (ulong offset = 0; offset < length; offset += ChunkSize)
            {
                ct.ThrowIfCancellationRequested();

                ulong remaining = length - offset;
                int take = (int)Math.Min(remaining, (ulong)(ChunkSize + overlap));
                var data = memory.Read(lo + offset, take);

                done += Math.Min(remaining, (ulong)ChunkSize);
                progress?.Report(done / total);
                if (data is null) continue;

                int maxStart = Math.Min(ChunkSize - 1, data.Length - pattern.Length);
                FindAll(data, pattern, maxStart, lo + offset, results, maxResults);
                if (results.Count >= maxResults) return results;
            }
        }

        progress?.Report(1);
        return results;
    }

    /// <summary>Potongan region yang beririsan dengan modul. executableOnly = hanya region kode.</summary>
    internal static List<(ulong Lo, ulong Hi)> ModuleRanges(ProcessMemory memory, ModuleInfo module, bool executableOnly)
    {
        ulong moduleStart = module.BaseAddress;
        ulong moduleEnd = moduleStart + module.Size;
        var ranges = new List<(ulong Lo, ulong Hi)>();

        foreach (var region in memory.EnumerateRegions(false))
        {
            ulong regionStart = region.BaseAddress;
            ulong regionEnd = regionStart + region.Size;

            if (regionEnd <= moduleStart) continue;
            if (regionStart >= moduleEnd) break;
            if (executableOnly && ((uint)region.Protect & 0xF0u) == 0) continue;

            ranges.Add((Math.Max(regionStart, moduleStart), Math.Min(regionEnd, moduleEnd)));
        }

        return ranges;
    }

    private static void FindAll(byte[] data, AobPattern pattern, int maxStart, ulong baseAddress,
        List<ulong> results, int maxResults)
    {
        if (maxStart < 0) return;

        var span = data.AsSpan();
        int anchor = pattern.AnchorIndex;
        byte anchorByte = pattern.Bytes[anchor];
        int pos = 0;

        while (pos <= maxStart)
        {
            int index = span.Slice(pos + anchor, maxStart - pos + 1).IndexOf(anchorByte);
            if (index < 0) return;

            int start = pos + index;
            if (pattern.MatchesAt(data, start))
            {
                results.Add(baseAddress + (ulong)start);
                if (results.Count >= maxResults) return;
            }
            pos = start + 1;
        }
    }
}