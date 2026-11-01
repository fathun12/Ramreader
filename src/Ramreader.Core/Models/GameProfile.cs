namespace Ramreader.Core.Models;

/// <summary>Profil game yang diekspor Ramreader dan dibaca trainer (fase 2).</summary>
public sealed class GameProfile
{
    public int FormatVersion { get; set; } = 1;
    public string GameName { get; set; } = "";
    public DateTime ExportedAt { get; set; }
    public List<SavedAddress> Addresses { get; set; } = new();
}