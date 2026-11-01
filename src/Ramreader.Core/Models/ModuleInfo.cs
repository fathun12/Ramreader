namespace Ramreader.Core.Models;

/// <summary>Satu modul (exe/dll) yang dimuat di dalam proses game.</summary>
public sealed record ModuleInfo(string Name, ulong BaseAddress, ulong Size)
{
    public bool Contains(ulong address) =>
        address >= BaseAddress && address < BaseAddress + Size;
}