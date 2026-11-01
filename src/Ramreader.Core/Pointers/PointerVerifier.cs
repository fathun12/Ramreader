using Ramreader.Core.Memory;

namespace Ramreader.Core.Pointers;

public static class PointerVerifier
{
    /// <summary>Hitung alamat akhir sebuah chain dengan base modul saat ini. Null kalau ada langkah yang gagal dibaca.</summary>
    public static ulong? Resolve(ProcessMemory memory, PointerChain chain)
    {
        var module = memory.Modules.FirstOrDefault(
            m => m.Name.Equals(chain.ModuleName, StringComparison.OrdinalIgnoreCase));
        if (module is null) return null;

        ulong location = module.BaseAddress + chain.ModuleOffset;
        if (chain.Offsets.Count == 0) return location;

        ulong? current = memory.ReadPointer(location);
        for (int i = 0; i < chain.Offsets.Count; i++)
        {
            if (current is null) return null;

            ulong address = current.Value + chain.Offsets[i];
            if (i == chain.Offsets.Count - 1) return address;

            current = memory.ReadPointer(address);
        }

        return null;
    }

    /// <summary>Sisakan hanya chain yang menghasilkan alamat target yang baru.</summary>
    public static List<PointerChain> Filter(ProcessMemory memory, IReadOnlyList<PointerChain> chains,
        ulong target, CancellationToken ct)
    {
        var survivors = new List<PointerChain>();
        for (int i = 0; i < chains.Count; i++)
        {
            if ((i & 0x3FF) == 0) ct.ThrowIfCancellationRequested();
            if (Resolve(memory, chains[i]) == target)
                survivors.Add(chains[i]);
        }
        return survivors;
    }
}