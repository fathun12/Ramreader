using System.Text;
using Ramreader.Core.Memory;

namespace Ramreader.Core.Debugging;

/// <summary>Satu objek karakter yang dicari. Base = alamat basis (register) dari ObjectTracer.</summary>
public sealed record ReverseTarget(ulong Base, string Label, bool IsEnemy);

/// <summary>Satu penunjuk ke objek. Location = tempat penunjuk disimpan, Delta = nilai penunjuk dikurangi Base.</summary>
public sealed record ReverseHit(ulong Location, int Index, long Delta);

public static class ReversePointerFinder
{
    public const ulong Back = 0x40;
    public const ulong Forward = 0x40;
    public const ulong ObjectSpan = 0x800;
    private const int MaxHits = 200_000;

    public static List<ReverseHit> Scan(ProcessMemory mem, IReadOnlyList<ReverseTarget> targets,
        IProgress<double>? progress, CancellationToken ct)
    {
        var hits = new List<ReverseHit>();
        if (targets.Count == 0) return hits;

        var order = Enumerable.Range(0, targets.Count).OrderBy(i => targets[i].Base).ToArray();
        var sorted = order.Select(i => targets[i].Base).ToArray();
        ulong lo = sorted[0] >= Back ? sorted[0] - Back : 0;
        ulong hi = sorted[^1] + Forward;

        double total = 0;
        foreach (var r in mem.EnumerateRegions(true)) total += (double)r.Size;
        double done = 0;

        foreach (var (addr, data) in mem.ReadChunks(true))
        {
            ct.ThrowIfCancellationRequested();
            int n = data.Length & ~7;
            for (int i = 0; i < n; i += 8)
            {
                ulong v = BitConverter.ToUInt64(data, i);
                if (v < lo || v > hi) continue;
                ulong vFrom = v >= Forward ? v - Forward : 0;
                int k = LowerBound(sorted, vFrom);
                if (k >= sorted.Length || sorted[k] > v + Back) continue;
                hits.Add(new ReverseHit(addr + (ulong)i, order[k], (long)(v - sorted[k])));
                if (hits.Count >= MaxHits) return hits;
            }
            done += data.Length;
            if (total > 0) progress?.Report(Math.Min(1.0, done / total));
        }
        return hits;
    }

    public static string Report(IReadOnlyList<ReverseHit> hits, IReadOnlyList<ReverseTarget> t, int gap = 0x200)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Objek: " + string.Join(", ", t.Select(x => $"{x.Label}=0x{x.Base:X}")));
        sb.AppendLine($"Total penunjuk: {hits.Count}");

        var inside = new List<(int Holder, ulong Off, ReverseHit H)>();
        var outside = new List<ReverseHit>();
        foreach (var h in hits)
        {
            int j = -1;
            for (int k = 0; k < t.Count; k++)
            {
                if (h.Location >= t[k].Base - Back && h.Location < t[k].Base + ObjectSpan) { j = k; break; }
            }
            if (j >= 0) inside.Add((j, h.Location - (t[j].Base - Back), h));
            else outside.Add(h);
        }

        sb.AppendLine($"[A] Penunjuk di dalam objek karakter: {inside.Count} (offset dihitung dari basis -0x40)");
        foreach (var x in inside.OrderBy(x => x.Holder).ThenBy(x => x.Off).Take(40))
            sb.AppendLine($"  {t[x.Holder].Label}+0x{x.Off:X} -> {t[x.H.Index].Label}{Sg(x.H.Delta)}");

        var sorted = outside.OrderBy(h => h.Location).ToList();
        var clusters = new List<List<ReverseHit>>();
        foreach (var h in sorted)
        {
            if (clusters.Count > 0 && h.Location - clusters[^1][^1].Location <= (ulong)gap)
                clusters[^1].Add(h);
            else
                clusters.Add(new List<ReverseHit> { h });
        }

        var multi = clusters
            .Where(c => c.Select(h => h.Index).Distinct().Count() >= 2)
            .OrderByDescending(c => c.Select(h => h.Index).Distinct().Count())
            .ThenBy(c => c[0].Location)
            .ToList();

        sb.AppendLine($"[B] Penampung luar dengan >=2 objek berbeda: {multi.Count} (penampung 1 objek: {clusters.Count - multi.Count})");
        foreach (var c in multi.Take(20))
        {
            var idx = c.Select(h => h.Index).Distinct().ToList();
            bool anyTeam = idx.Any(i => !t[i].IsEnemy);
            bool anyEnemy = idx.Any(i => t[i].IsEnemy);
            string jenis = anyTeam && anyEnemy ? "CAMPUR" : anyEnemy ? "MUSUH-SAJA" : "TIM-SAJA";
            sb.AppendLine($"  @0x{c[0].Location:X} {jenis}: " +
                string.Join(" ", c.Take(12).Select(h => $"+0x{h.Location - c[0].Location:X}={t[h.Index].Label}{Sg(h.Delta)}")));
        }
        return sb.ToString();
    }

    private static string Sg(long d) => d >= 0 ? $"+0x{d:X}" : $"-0x{-d:X}";

    private static int LowerBound(ulong[] a, ulong x)
    {
        int l = 0, h = a.Length;
        while (l < h)
        {
            int m = (l + h) >> 1;
            if (a[m] < x) l = m + 1; else h = m;
        }
        return l;
    }
}