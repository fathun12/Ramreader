using System.IO;
using Ramreader.Core.Pointers;

namespace Ramreader.App.Services;

public sealed record StoredChains(List<PointerChain> Chains, int Rounds);

/// <summary>Menyimpan jalur kandidat per game di %AppData%\Ramreader\chains, supaya tahan restart game dan aplikasi.</summary>
public sealed class ChainStore
{
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ramreader", "chains");

    private static string PathFor(string processName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            processName = processName.Replace(c, '_');
        return Path.Combine(Dir, processName + ".txt");
    }

    public StoredChains Load(string processName)
    {
        var chains = new List<PointerChain>();
        int rounds = 0;

        try
        {
            var path = PathFor(processName);
            if (File.Exists(path))
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (line.StartsWith("#rounds=", StringComparison.Ordinal))
                    {
                        int.TryParse(line.AsSpan(8), out rounds);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;
                    if (PointerChain.TryParse(line, out var chain)) chains.Add(chain);
                }
            }
        }
        catch
        {
            // File rusak atau terkunci: anggap kosong.
        }

        return new StoredChains(chains, rounds);
    }

    public void Save(string processName, IReadOnlyList<PointerChain> chains, int rounds)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllLines(PathFor(processName),
            chains.Select(c => c.Serialize()).Prepend($"#rounds={rounds}"));
    }

    public void Delete(string processName)
    {
        try
        {
            var path = PathFor(processName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Abaikan.
        }
    }
}