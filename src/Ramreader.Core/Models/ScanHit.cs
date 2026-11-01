namespace Ramreader.Core.Models;

/// <summary>
/// Satu alamat kandidat. Value dan Previous disimpan sebagai bit mentah (ulong)
/// supaya semua tipe (int, float, dst) bisa memakai struktur yang sama.
/// </summary>
public readonly record struct ScanHit(ulong Address, ulong Value, ulong Previous);