namespace Ramreader.Core.Debugging;

/// <summary>Isi memori satu objek karakter beserta peran yang ditandai user (Tim atau Musuh).</summary>
public sealed record ObjectSample(ulong Base, bool IsTeam, byte[] Data);

/// <summary>Satu offset yang nilainya sama di seluruh Tim tapi tidak pernah sama dengan Musuh.</summary>
public sealed record MarkerCandidate(int Offset, uint TeamValue, string EnemyValues);

public static class ObjectComparer
{
    /// <summary>Bandingkan per 4 byte. Kandidat penanda: nilai Tim seragam, dan tidak ada Musuh yang bernilai sama.</summary>
    public static List<MarkerCandidate> Compare(IReadOnlyList<ObjectSample> samples)
    {
        var result = new List<MarkerCandidate>();
        var team = samples.Where(s => s.IsTeam).ToList();
        var enemy = samples.Where(s => !s.IsTeam).ToList();
        if (team.Count == 0 || enemy.Count == 0) return result;

        int size = samples.Min(s => s.Data.Length) & ~3;

        for (int off = 0; off < size; off += 4)
        {
            uint t = BitConverter.ToUInt32(team[0].Data, off);

            bool uniform = true;
            foreach (var s in team)
            {
                if (BitConverter.ToUInt32(s.Data, off) != t) { uniform = false; break; }
            }
            if (!uniform) continue;

            bool separated = true;
            var enemyValues = new SortedSet<uint>();
            foreach (var s in enemy)
            {
                uint v = BitConverter.ToUInt32(s.Data, off);
                if (v == t) { separated = false; break; }
                enemyValues.Add(v);
            }
            if (!separated) continue;

            result.Add(new MarkerCandidate(off, t,
                string.Join(", ", enemyValues.Take(6).Select(v => $"0x{v:X}"))));
        }

        return result;
    }
}