using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Ramreader.Core.Scanning;

/// <summary>Pola byte dengan wildcard, mis. "48 8B ?? 58".</summary>
public sealed class AobPattern
{
    public const int MaxLength = 256;

    private AobPattern(byte[] bytes, bool[] wildcard, int anchorIndex)
    {
        Bytes = bytes;
        Wildcard = wildcard;
        AnchorIndex = anchorIndex;
    }

    public byte[] Bytes { get; }
    public bool[] Wildcard { get; }
    public int Length => Bytes.Length;

    /// <summary>Indeks byte pasti pertama (dipakai untuk loncat cepat saat mencari).</summary>
    public int AnchorIndex { get; }

    /// <summary>Buat pola dari byte mentah; indeks di wildcardIndexes menjadi ??. Null kalau semuanya wildcard.</summary>
    public static AobPattern? Create(byte[] data, IReadOnlyCollection<int> wildcardIndexes)
    {
        if (data.Length == 0 || data.Length > MaxLength) return null;

        var wild = new bool[data.Length];
        foreach (int i in wildcardIndexes)
            if (i >= 0 && i < wild.Length) wild[i] = true;

        int anchor = Array.FindIndex(wild, w => !w);
        return anchor < 0 ? null : new AobPattern((byte[])data.Clone(), wild, anchor);
    }

    public static bool TryParse(string? text, [NotNullWhen(true)] out AobPattern? pattern, out string error)
    {
        pattern = null;
        error = "";

        var tokens = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            error = "Pola masih kosong. Contoh: 48 8B 05 ?? ?? ?? ?? 48 85 C0";
            return false;
        }
        if (tokens.Length > MaxLength)
        {
            error = $"Pola terlalu panjang (maksimal {MaxLength} byte).";
            return false;
        }

        var bytes = new byte[tokens.Length];
        var wild = new bool[tokens.Length];
        int anchor = -1;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (token == "?" || token == "??")
            {
                wild[i] = true;
                continue;
            }

            if (token.Length != 2 ||
                !byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
            {
                error = $"\"{token}\" bukan byte yang valid. Tulis dua digit hex (mis. 48) atau ?? untuk wildcard.";
                return false;
            }

            if (anchor < 0) anchor = i;
        }

        if (anchor < 0)
        {
            error = "Pola hanya berisi ??. Harus ada minimal satu byte pasti.";
            return false;
        }

        pattern = new AobPattern(bytes, wild, anchor);
        return true;
    }

    public bool MatchesAt(byte[] data, int offset)
    {
        if (offset < 0 || offset + Bytes.Length > data.Length) return false;
        for (int i = 0; i < Bytes.Length; i++)
            if (!Wildcard[i] && data[offset + i] != Bytes[i]) return false;
        return true;
    }

    public override string ToString() =>
        string.Join(' ', Bytes.Select((b, i) => Wildcard[i] ? "??" : b.ToString("X2")));
}