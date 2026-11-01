namespace Ramreader.Core.Models;

/// <summary>
/// Signature (pola byte) untuk menemukan kembali kode game setelah update.
/// DispOffset >= 0: pola memuat operand RIP-relative, jadi alamat statisnya bisa dihitung
/// (lihat SignatureResolver). DispOffset = -1: pola hanya menemukan lokasi kode.
/// </summary>
public sealed class SignatureRef
{
    /// <summary>Pola AOB, mis. "48 8B 05 ?? ?? ?? ?? 48 85 C0".</summary>
    public string Pattern { get; set; } = "";

    /// <summary>Posisi byte (dari awal pola) tempat disp32 RIP-relative berada. -1 = tidak ada.</summary>
    public int DispOffset { get; set; } = -1;

    /// <summary>Posisi byte (dari awal pola) akhir instruksi. Alamat RIP = awal pola + nilai ini.</summary>
    public int InstructionEnd { get; set; }

    /// <summary>Tambahan ke alamat hasil RIP untuk sampai ke alamat statis jalur (kalau kodenya merujuk ke awal tabel).</summary>
    public ulong StaticDelta { get; set; }

    /// <summary>Offset jalur setelah alamat statis, hex dipisah koma (kosong = alamat statis itu sendiri adalah nilainya).</summary>
    public string Offsets { get; set; } = "";

    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}