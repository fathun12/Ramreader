using System.Globalization;
using Ramreader.Core.Models;

namespace Ramreader.Core.Scanning;

/// <summary>Konversi antara teks, bit mentah, dan tampilan untuk tiap tipe nilai.</summary>
public static class ValueCodec
{
    public static int SizeOf(ScanValueType type) => type switch
    {
        ScanValueType.Int32 or ScanValueType.Float => 4,
        _ => 8
    };

    /// <summary>Ubah teks input user jadi bit mentah. Koma desimal diterima sebagai titik.</summary>
    public static bool TryParse(ScanValueType type, string text, out ulong raw)
    {
        raw = 0;
        text = (text ?? "").Trim().Replace(',', '.');
        var culture = CultureInfo.InvariantCulture;

        switch (type)
        {
            case ScanValueType.Int32:
                if (!int.TryParse(text, NumberStyles.Integer, culture, out var i)) return false;
                raw = (uint)i;
                return true;

            case ScanValueType.Float:
                if (!float.TryParse(text, NumberStyles.Float, culture, out var f)) return false;
                raw = BitConverter.SingleToUInt32Bits(f);
                return true;

            case ScanValueType.Int64:
                if (!long.TryParse(text, NumberStyles.Integer, culture, out var l)) return false;
                raw = (ulong)l;
                return true;

            case ScanValueType.Double:
                if (!double.TryParse(text, NumberStyles.Float, culture, out var d)) return false;
                raw = BitConverter.DoubleToUInt64Bits(d);
                return true;
        }

        return false;
    }

    public static string Format(ScanValueType type, ulong raw)
    {
        var culture = CultureInfo.InvariantCulture;
        return type switch
        {
            ScanValueType.Int32 => ((int)(uint)raw).ToString(culture),
            ScanValueType.Float => BitConverter.UInt32BitsToSingle((uint)raw).ToString("G7", culture),
            ScanValueType.Int64 => ((long)raw).ToString(culture),
            _ => BitConverter.UInt64BitsToDouble(raw).ToString("G15", culture)
        };
    }

    /// <summary>Bandingkan dua nilai sesuai tipenya (untuk mode bertambah / berkurang).</summary>
    public static int Compare(ScanValueType type, ulong a, ulong b) => type switch
    {
        ScanValueType.Int32 => ((int)(uint)a).CompareTo((int)(uint)b),
        ScanValueType.Float => BitConverter.UInt32BitsToSingle((uint)a).CompareTo(BitConverter.UInt32BitsToSingle((uint)b)),
        ScanValueType.Int64 => ((long)a).CompareTo((long)b),
        _ => BitConverter.UInt64BitsToDouble(a).CompareTo(BitConverter.UInt64BitsToDouble(b))
    };

    /// <summary>True kalau nilai berada di antara min dan max (inklusif). NaN tidak pernah masuk rentang.</summary>
    public static bool InRange(ScanValueType type, ulong raw, ulong min, ulong max)
    {
        switch (type)
        {
            case ScanValueType.Int32:
            {
                int v = (int)(uint)raw;
                return v >= (int)(uint)min && v <= (int)(uint)max;
            }
            case ScanValueType.Float:
            {
                float v = BitConverter.UInt32BitsToSingle((uint)raw);
                return v >= BitConverter.UInt32BitsToSingle((uint)min)
                    && v <= BitConverter.UInt32BitsToSingle((uint)max);
            }
            case ScanValueType.Int64:
            {
                long v = (long)raw;
                return v >= (long)min && v <= (long)max;
            }
            default:
            {
                double v = BitConverter.UInt64BitsToDouble(raw);
                return v >= BitConverter.UInt64BitsToDouble(min)
                    && v <= BitConverter.UInt64BitsToDouble(max);
            }
        }
    }

    public static ulong ReadRaw(byte[] data, int offset, int size) =>
        size == 4 ? BitConverter.ToUInt32(data, offset) : BitConverter.ToUInt64(data, offset);
}