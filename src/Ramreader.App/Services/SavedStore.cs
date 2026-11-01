using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ramreader.Core.Models;

namespace Ramreader.App.Services;

public sealed class SavedStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ramreader", "sessions");

    private static string PathFor(string processName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            processName = processName.Replace(c, '_');
        return Path.Combine(Dir, processName + ".json");
    }

    public List<SavedAddress> Load(string processName)
    {
        try
        {
            var path = PathFor(processName);
            if (!File.Exists(path)) return new List<SavedAddress>();
            return JsonSerializer.Deserialize<List<SavedAddress>>(File.ReadAllText(path), Options)
                   ?? new List<SavedAddress>();
        }
        catch
        {
            // File rusak atau terkunci: anggap kosong.
            return new List<SavedAddress>();
        }
    }

    public void Save(string processName, IReadOnlyList<SavedAddress> items)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(PathFor(processName), JsonSerializer.Serialize(items, Options));
    }

    public void Export(string path, GameProfile profile) =>
        File.WriteAllText(path, JsonSerializer.Serialize(profile, Options));
}