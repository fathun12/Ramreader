using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Core.Scanning;

namespace Ramreader.Core.Pointers;

public enum ResolveStatus
{
    Ok,                  // alamat ketemu dan nilainya terbaca
    SignatureSearching,  // semua jalur putus, signature sedang dicari di latar
    Failed               // tidak ada jalur atau signature yang bisa dibaca
}

public enum ResolveSource { None, Chain, Signature }

/// <param name="Index">Urutan jalur/signature yang berhasil (0 = utama).</param>
public sealed record ResolveResult(ResolveStatus Status, ResolveSource Source, int Index, ulong Address, ulong Raw)
{
    public static ResolveResult NotResolved { get; } =
        new(ResolveStatus.Failed, ResolveSource.None, -1, 0, 0);

    public static ResolveResult StillSearching { get; } =
        new(ResolveStatus.SignatureSearching, ResolveSource.None, -1, 0, 0);
}

/// <summary>
/// Satu-satunya tempat logika "coba tiap jalur berurutan, kalau semua putus coba signature".
/// Dipakai Ramreader (kartu Tersimpan) dan Trainer.
/// </summary>
public static class AddressResolver
{
    public static ResolveResult Resolve(ProcessMemory memory, IReadOnlyList<PointerChain> chains,
        IReadOnlyList<SignatureRef> signatures, ScanValueType type)
    {
        int size = ValueCodec.SizeOf(type);

        for (int i = 0; i < chains.Count; i++)
        {
            var address = PointerVerifier.Resolve(memory, chains[i]);
            if (address is null) continue;

            var data = memory.Read(address.Value, size);
            if (data is null) continue;

            return new ResolveResult(ResolveStatus.Ok, ResolveSource.Chain, i,
                address.Value, ValueCodec.ReadRaw(data, 0, size));
        }

        // Semua jalur putus: coba signature (tahan terhadap update game).
        for (int s = 0; s < signatures.Count; s++)
        {
            var result = SignatureResolver.Resolve(memory, signatures[s]);
            if (result.State == SignatureState.Searching) return ResolveResult.StillSearching;
            if (result.Address is null) continue;

            var bytes = memory.Read(result.Address.Value, size);
            if (bytes is null) continue;

            return new ResolveResult(ResolveStatus.Ok, ResolveSource.Signature, s,
                result.Address.Value, ValueCodec.ReadRaw(bytes, 0, size));
        }

        return ResolveResult.NotResolved;
    }
}