using System.Runtime.InteropServices;
using Ramreader.Core.Memory;

namespace Ramreader.Core.Pointers;

public sealed record PointerScanProgress(double Fraction, string Message);

public sealed record PointerScanResult(List<PointerChain> Chains, bool Truncated);

/// <summary>
/// Mencari rantai pointer dari modul utama (exe game) menuju satu alamat target.
/// Bekerja per level: level 1 mencari pointer yang menunjuk ke target, level 2 mencari
/// pointer yang menunjuk ke pointer-pointer itu, dst, sampai ada yang berada di dalam exe.
/// </summary>
public sealed class PointerScanner
{
    public const int MaxFrontier = 100_000;
    public const int MaxResults = 100_000;

    // Filter cepat berbasis halaman 4 KB (2 MB bit array). Hanya nilai yang lolos filter ini
    // yang dicek dengan binary search, jadi memindai GB memori tetap masuk akal.
    private const ulong BloomMask = (1UL << 24) - 1;
    private const int BloomWords = (1 << 24) / 64;

    private sealed class Node
    {
        public ulong Addr;
        public ulong Off;
        public Node? Parent;
    }

    public PointerScanResult Scan(ProcessMemory memory, ulong target, int maxDepth, ulong maxOffset,
        IProgress<PointerScanProgress>? progress, CancellationToken ct)
    {
        var main = memory.MainModule
            ?? throw new InvalidOperationException("Modul utama game tidak terbaca. Coba jalankan sebagai Administrator.");

        var results = new List<PointerChain>();
        bool truncated = false;

        // Target yang kebetulan statis (ada di dalam exe) tidak butuh pointer sama sekali.
        if (main.Contains(target))
            results.Add(new PointerChain(main.Name, target - main.BaseAddress, Array.Empty<ulong>()));

        double total = memory.EnumerateRegions(true).Sum(r => (double)r.Size);
        if (total <= 0) total = 1;

        var frontier = new List<Node> { new Node { Addr = target } };
        var visited = new HashSet<ulong> { target };

        for (int level = 1; level <= maxDepth && frontier.Count > 0; level++)
        {
            ct.ThrowIfCancellationRequested();

            var sorted = frontier.OrderBy(n => n.Addr).ToArray();
            var keys = sorted.Select(n => n.Addr).ToArray();
            ulong maxKey = keys[^1];
            ulong low = keys[0] > maxOffset ? keys[0] - maxOffset : 0;
            var bloom = BuildBloom(keys, maxOffset);

            var next = new Dictionary<ulong, Node>();
            double done = 0;

            foreach (var (address, data) in memory.ReadChunks(true))
            {
                ct.ThrowIfCancellationRequested();

                var values = MemoryMarshal.Cast<byte, ulong>(data.AsSpan(0, data.Length & ~7));

                for (int i = 0; i < values.Length; i++)
                {
                    ulong p = values[i];
                    if (p < low || p > maxKey) continue;

                    int bit = (int)((p >> 12) & BloomMask);
                    if ((bloom[bit >> 6] & (1UL << (bit & 63))) == 0) continue;

                    int idx = Array.BinarySearch(keys, p);
                    if (idx < 0) idx = ~idx;
                    if (idx >= keys.Length || keys[idx] - p > maxOffset) continue;

                    ulong location = address + (ulong)i * 8;
                    var node = new Node { Addr = location, Off = keys[idx] - p, Parent = sorted[idx] };

                    if (main.Contains(location))
                    {
                        if (results.Count < MaxResults) results.Add(ToChain(main.Name, main.BaseAddress, node));
                        else truncated = true;
                    }
                    else if (visited.Contains(location))
                    {
                        continue;
                    }
                    else if (next.Count < MaxFrontier)
                    {
                        next.TryAdd(location, node);
                    }
                    else
                    {
                        truncated = true;
                    }
                }

                done += data.Length;
                double fraction = ((level - 1) + Math.Min(done / total, 1.0)) / maxDepth;
                progress?.Report(new PointerScanProgress(fraction,
                    $"Level {level} dari {maxDepth}  •  {results.Count:N0} jalur statis  •  {next.Count:N0} kandidat lanjut"));
            }

            foreach (var key in next.Keys) visited.Add(key);
            frontier = next.Values.ToList();
        }

        // Jalur terpendek dulu, lalu offset modul terkecil.
        results.Sort((a, b) =>
        {
            int c = a.Offsets.Count.CompareTo(b.Offsets.Count);
            return c != 0 ? c : a.ModuleOffset.CompareTo(b.ModuleOffset);
        });

        progress?.Report(new PointerScanProgress(1, "Selesai"));
        return new PointerScanResult(results, truncated);
    }

    private static PointerChain ToChain(string moduleName, ulong moduleBase, Node node)
    {
        var offsets = new List<ulong>();
        for (var c = node; c.Parent != null; c = c.Parent)
            offsets.Add(c.Off);

        return new PointerChain(moduleName, node.Addr - moduleBase, offsets);
    }

    private static ulong[] BuildBloom(ulong[] keys, ulong maxOffset)
    {
        var bloom = new ulong[BloomWords];
        foreach (var key in keys)
        {
            ulong lowest = key > maxOffset ? key - maxOffset : 0;
            for (ulong page = lowest >> 12; page <= key >> 12; page++)
            {
                int bit = (int)(page & BloomMask);
                bloom[bit >> 6] |= 1UL << (bit & 63);
            }
        }
        return bloom;
    }
}