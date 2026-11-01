namespace Ramreader.Core.Models;

/// <summary>Satu blok memori yang bisa dibaca di proses target.</summary>
public readonly record struct MemoryRegion(ulong BaseAddress, ulong Size, uint Protect, bool IsWritable);