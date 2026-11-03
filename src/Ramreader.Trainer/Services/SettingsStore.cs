using System.IO;
using System.Text.Json;

namespace Ramreader.Trainer.Services;

public sealed class FeatureSettings
{
    public string DisplayName { get; set; } = "";
    public string Min { get; set; } = "0";
    public string Max { get; set; } = "1000000000";
    public string Preset { get; set; } = "";
    public string HotkeySet { get; set; } = "";
    public string HotkeyFreeze { get; set; } = "";
    public bool AutoFreeze { get; set; }
}

public sealed class PatchSettings
{
    public string Hotkey { get; set; } = "";
}

public sealed class GameSettings
{
    public int FreezeIntervalMs { get; set; } = 10;
    public Dictionary<string, FeatureSettings> Features { get; set; } = new();
    public Dictionary<string, PatchSettings> Patches { get; set; } = new();
}

/// <summary>Pengaturan trainer per game, terpisah dari profil Ramreader (ekspor ulang profil tidak menghapusnya).</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ramreader.Trainer", "settings");

    private static string PathFor(string gameName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            gameName = gameName.Replace(c, '_');
        return Path.Combine(Dir, gameName + ".json");
    }

    public static GameSettings Load(string gameName)
    {
        try
        {
            var path = PathFor(gameName);
            if (!File.Exists(path)) return new GameSettings();
            return JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path), Options) ?? new GameSettings();
        }
        catch
        {
            return new GameSettings();
        }
    }

    public static void Save(string gameName, GameSettings settings)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(PathFor(gameName), JsonSerializer.Serialize(settings, Options));
    }
}