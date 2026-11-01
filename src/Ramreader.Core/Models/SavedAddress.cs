namespace Ramreader.Core.Models;

/// <summary>Satu alamat yang disimpan user: label, tipe nilai, dan jalur-jalur menuju alamatnya.</summary>
public sealed class SavedAddress
{
    public string Label { get; set; } = "";
    public ScanValueType ValueType { get; set; }

    /// <summary>Berapa kali jalur ini sudah lolos verifikasi setelah game direstart.</summary>
    public int VerifiedRounds { get; set; }

    /// <summary>Ukuran exe saat disimpan. Trainer bisa memakainya untuk mendeteksi game sudah di-update.</summary>
    public ulong MainModuleSize { get; set; }

    public DateTime SavedAt { get; set; }

    /// <summary>Jalur dalam format PointerChain.Serialize(), jalur terbaik di urutan pertama.</summary>
    public List<string> Chains { get; set; } = new();
}