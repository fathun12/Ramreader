using Iced.Intel;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Core.Scanning;
using Decoder = Iced.Intel.Decoder;

namespace Ramreader.Core.Debugging;

public sealed record CodePatchResult(CodePatch? Patch, string Message);

/// <summary>
/// Membuat CodePatch (NOP) dari alamat instruksi penulis. Byte instruksinya dijadikan ??
/// supaya pola tetap ditemukan walau game sedang dalam keadaan ter-patch. Operand relatif
/// (call/jmp, RIP-relative) di sekitarnya juga dijadikan ?? supaya tahan perubahan kecil.
/// </summary>
public static class CodePatchMaker
{
    private static readonly int[] Pre = { 0, 4, 8, 12, 16 };
    private static readonly int[] Post = { 4, 8, 12, 16, 24, 32 };

    public static CodePatchResult Make(ProcessMemory memory, ModuleInfo module, ulong address, string label,
        IProgress<double>? progress, CancellationToken ct)
    {
        ulong moduleEnd = module.BaseAddress + module.Size;
        if (address < module.BaseAddress || address >= moduleEnd)
            return Fail("Instruksi ini berada di luar modul utama game, jadi tidak bisa dibuat patch.");

        var head = memory.Read(address, 15);
        if (head is null) return Fail("Gagal membaca kode di alamat itu.");

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(head));
        decoder.IP = address;
        var instr = decoder.Decode();
        if (instr.IsInvalid) return Fail("Byte di alamat itu bukan instruksi yang valid.");
        if (instr.FlowControl != FlowControl.Next)
            return Fail("Instruksi ini cabang/call, bukan penulis nilai. Pilih kandidat lain.");

        int length = instr.Length;
        var original = head[..length];

        var windows = new List<(int Pre, int Post)>();
        foreach (int pre in Pre)
            foreach (int post in Post)
                windows.Add((pre, post));
        windows.Sort((a, b) => (a.Pre + a.Post).CompareTo(b.Pre + b.Post));

        int lastCount = 0;
        for (int k = 0; k < windows.Count; k++)
        {
            ct.ThrowIfCancellationRequested();

            var (pre, post) = windows[k];
            if (address - module.BaseAddress < (ulong)pre) continue;
            if (address + (ulong)length + (ulong)post > moduleEnd) continue;

            ulong start = address - (ulong)pre;
            var bytes = memory.Read(start, pre + length + post);
            if (bytes is null) continue;

            var wild = new List<int>();
            for (int i = pre; i < pre + length; i++) wild.Add(i);

            if (pre > 0)
            {
                int s = FindAlignment(bytes, pre);
                if (s >= 0) AddRelocWildcards(bytes, s, pre, start, wild);
            }
            AddRelocWildcards(bytes, pre + length, bytes.Length, start, wild);

            var pattern = AobPattern.Create(bytes, wild);
            if (pattern is null) continue;

            var hits = AobScanner.Scan(memory, module, pattern, 2, null, ct);
            progress?.Report((k + 1.0) / windows.Count);
            lastCount = hits.Count;

            if (hits.Count == 1 && hits[0] == start)
            {
                var patch = new CodePatch
                {
                    Label = label,
                    Pattern = pattern.ToString(),
                    InstructionOffset = pre,
                    OriginalBytes = CodePatch.ToHex(original),
                    PatchBytes = CodePatch.ToHex(Enumerable.Repeat((byte)0x90, length)),
                    Note = "Instruksi diganti NOP. Signature mengabaikan byte instruksi itu sendiri.",
                    CreatedAt = DateTime.Now
                };
                return new CodePatchResult(patch,
                    $"Patch dibuat: instruksi {length} byte ({patch.OriginalBytes}) diganti NOP. Pola unik, {pattern.Length} byte.");
            }
        }

        return Fail(lastCount > 1
            ? "Tidak ada pola yang unik di sekitar instruksi ini. Coba kandidat lain."
            : "Tidak bisa membuat pola dari kode di sekitar instruksi ini. Coba kandidat lain.");
    }

    /// <summary>Cari titik awal supaya decode berurutan mendarat tepat di indeks target.</summary>
    private static int FindAlignment(byte[] bytes, int target)
    {
        for (int s = 0; s < target; s++)
        {
            int i = s;
            while (i < target)
            {
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes, i, bytes.Length - i));
                var ins = decoder.Decode();
                if (ins.IsInvalid) { i = -1; break; }
                i += ins.Length;
            }
            if (i == target) return s;
        }
        return -1;
    }

    /// <summary>Jadikan ?? operand yang berubah antar build: displacement RIP-relative dan target call/jmp.</summary>
    private static void AddRelocWildcards(byte[] bytes, int from, int to, ulong baseAddress, List<int> wild)
    {
        int index = from;
        while (index < to && index < bytes.Length)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes, index, bytes.Length - index));
            decoder.IP = baseAddress + (ulong)index;
            var ins = decoder.Decode();
            if (ins.IsInvalid) return;

            var offsets = decoder.GetConstantOffsets(ins);
            if (ins.IsIPRelativeMemoryOperand && offsets.HasDisplacement)
                AddRange(wild, index + offsets.DisplacementOffset, offsets.DisplacementSize, bytes.Length);

            if (ins.Op0Kind is OpKind.NearBranch64 or OpKind.NearBranch32 or OpKind.NearBranch16 && offsets.HasImmediate)
                AddRange(wild, index + offsets.ImmediateOffset, offsets.ImmediateSize, bytes.Length);

            index += ins.Length;
        }
    }

    private static void AddRange(List<int> wild, int start, int size, int limit)
    {
        for (int i = start; i < start + size && i < limit; i++) wild.Add(i);
    }

    private static CodePatchResult Fail(string message) => new(null, message);
}