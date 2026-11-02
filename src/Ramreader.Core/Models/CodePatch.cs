using System.Globalization;

namespace Ramreader.Core.Models;

/// <summary>Satu code patch: instruksi di kode game yang diganti (mis. NOP) supaya nilai tidak pernah turun.</summary>
public sealed class CodePatch
{
    public string Label { get; set; } = "God Mode";

    /// <summary>Pola AOB di modul utama. Byte instruksi yang diganti ditulis ?? supaya pola tetap cocok saat sudah ter-patch.</summary>
    public string Pattern { get; set; } = "";

    /// <summary>Posisi instruksi dari awal pola.</summary>
    public int InstructionOffset { get; set; }

    /// <summary>Byte asli instruksi (hex dipisah spasi). Dicek sebelum menulis.</summary>
    public string OriginalBytes { get; set; } = "";

    /// <summary>Byte pengganti (bawaan: 90 sebanyak panjang instruksi).</summary>
    public string PatchBytes { get; set; } = "";

    public string Note { get; set; } = "";

    /// <summary>Kosong atau "Cave" = hanya karakter pemain yang kebal (bawaan). "Team" = seluruh tim pemain kebal, musuh tidak (dikenali dari penanda di objek). "Nop" = NOP polos, semua karakter ikut kebal.</summary>
    public string Mode { get; set; } = "";

    /// <summary>Mode "Team": offset penanda sisi di dalam objek karakter (hasil halaman Objek Karakter). Nilainya dibaca dari objek pemain saat game berjalan, bukan disimpan.</summary>
    public int MarkerOffset { get; set; } = 0x44;
    public DateTime CreatedAt { get; set; }

    public static string ToHex(IEnumerable<byte> bytes) => string.Join(' ', bytes.Select(b => b.ToString("X2")));

    public static bool TryParseBytes(string? text, out byte[] bytes)
    {
        var tokens = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        bytes = new byte[tokens.Length];
        if (tokens.Length == 0) return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            if (!byte.TryParse(tokens[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                return false;
        }
        return true;
    }
}