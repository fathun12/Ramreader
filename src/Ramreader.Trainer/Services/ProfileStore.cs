using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ramreader.Core.Models;

namespace Ramreader.Trainer.Services;

/// <summary>Satu profil game. Origin kosong = dari sesi Ramreader; terisi = nama file yang dibuka manual.</summary>
public sealed record ProfileEntry(string GameName, string Origin, List<SavedAddress> Addresses)
{
    public string DisplayName => Origin.Length == 0 ? GameName : $"{GameName} ({Origin})";
}

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static string SessionsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ramreader", "sessions");

    /// <summary>Semua game yang alamatnya sudah disimpan di Ramreader.</summary>
    public static List<ProfileEntry> LoadSessions()
    {
        var list = new List<ProfileEntry>();
        if (!Directory.Exists(SessionsDir)) return list;

        foreach (var file in Directory.GetFiles(SessionsDir, "*.json"))
        {
            var entry = LoadFile(file, "");
            if (entry is not null && entry.Addresses.Count > 0) list.Add(entry);
        }

        return list.OrderBy(p => p.GameName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Baca file sesi (array) atau file ekspor profil (objek GameProfile). Null kalau tidak valid.</summary>
    public static ProfileEntry? LoadFile(string path, string origin)
    {
        try
        {
            string text = File.ReadAllText(path);

            if (text.TrimStart().StartsWith('['))
            {
                var addresses = JsonSerializer.Deserialize<List<SavedAddress>>(text, Options);
                return addresses is null
                    ? null
                    : new ProfileEntry(Path.GetFileNameWithoutExtension(path), origin, addresses);
            }

            var profile = JsonSerializer.Deserialize<GameProfile>(text, Options);
            if (profile is null) return null;

            string name = string.IsNullOrWhiteSpace(profile.GameName)
                ? Path.GetFileNameWithoutExtension(path)
                : profile.GameName;
            return new ProfileEntry(name, origin, profile.Addresses);
        }
        catch
        {
            return null;
        }
    }
}