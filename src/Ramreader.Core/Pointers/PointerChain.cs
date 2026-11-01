using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Ramreader.Core.Pointers;

/// <summary>
/// Jalur menuju satu alamat.
/// Offsets kosong: alamatnya langsung ModuleName+ModuleOffset (statis).
/// Offsets terisi: baca pointer di ModuleName+ModuleOffset, tambah Offsets[0]; kecuali itu offset
/// terakhir, baca pointer di hasilnya, tambah offset berikutnya, dst. Hasil akhir = alamat target.
/// </summary>
public sealed record PointerChain(string ModuleName, ulong ModuleOffset, IReadOnlyList<ulong> Offsets)
{
    /// <summary>Satu baris teks untuk disimpan ke file: Modul|RelHex|off1,off2,...</summary>
    public string Serialize() =>
        $"{ModuleName}|{ModuleOffset:X}|{string.Join(',', Offsets.Select(o => o.ToString("X")))}";

    /// <summary>Tampilan ramah: Game.exe+4A12B80 → +30 → +1F8</summary>
    public string Describe()
    {
        var text = $"{ModuleName}+{ModuleOffset:X}";
        if (Offsets.Count == 0) return text + " (langsung)";
        return text + string.Concat(Offsets.Select(o => $" → +{o:X}"));
    }

    public static bool TryParse(string line, [NotNullWhen(true)] out PointerChain? chain)
    {
        chain = null;
        var parts = line.Split('|');
        if (parts.Length != 3) return false;

        if (!ulong.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var moduleOffset))
            return false;

        var offsets = new List<ulong>();
        if (parts[2].Length > 0)
        {
            foreach (var piece in parts[2].Split(','))
            {
                if (!ulong.TryParse(piece, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
                    return false;
                offsets.Add(offset);
            }
        }

        chain = new PointerChain(parts[0], moduleOffset, offsets);
        return true;
    }
}