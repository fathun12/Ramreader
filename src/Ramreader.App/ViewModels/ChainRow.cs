using Ramreader.Core.Pointers;

namespace Ramreader.App.ViewModels;

/// <summary>Satu baris jalur di daftar Jalur Stabil.</summary>
public sealed class ChainRow
{
    public ChainRow(PointerChain chain, string resolvedText)
    {
        Chain = chain;
        ResolvedText = resolvedText;
    }

    public PointerChain Chain { get; }
    public string Description => Chain.Describe();
    public string LevelText => Chain.Offsets.Count == 0 ? "langsung" : $"{Chain.Offsets.Count} level";

    /// <summary>Alamat yang dihasilkan jalur ini di game yang sedang jalan ("-" kalau tidak terhubung / gagal).</summary>
    public string ResolvedText { get; }
}