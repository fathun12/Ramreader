using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Core.Scanning;

namespace Ramreader.Core.Debugging;

public enum PatchState { Original, Patched, Mismatch, Unreadable }

/// <summary>Menemukan lokasi patch lewat signature dan memeriksa byte di sana. Dipakai Trainer.</summary>
public static class CodePatchLocator
{
    /// <summary>Pemindaian modul utama (bisa beberapa detik): jalankan di thread latar.</summary>
    public static (ulong? Address, string Message) Find(ProcessMemory memory, CodePatch patch, CancellationToken ct)
    {
        var module = memory.MainModule;
        if (module is null) return (null, "Modul utama game tidak terbaca.");

        if (!AobPattern.TryParse(patch.Pattern, out var pattern, out string error))
            return (null, "Pola patch rusak: " + error);

        var hits = AobScanner.Scan(memory, module, pattern, 2, null, ct);
        if (hits.Count == 0)
            return (null, "Lokasi kode tidak ditemukan. Game kemungkinan sudah di-update; buat ulang patch di Ramreader.");
        if (hits.Count > 1)
            return (null, "Pola patch cocok di lebih dari satu tempat. Buat ulang patch di Ramreader.");

        return (hits[0] + (ulong)patch.InstructionOffset, "");
    }

    public static PatchState Inspect(ProcessMemory memory, CodePatch patch, ulong address, out string detail)
    {
        detail = "";

        if (!CodePatch.TryParseBytes(patch.OriginalBytes, out var original) ||
            !CodePatch.TryParseBytes(patch.PatchBytes, out var patched) ||
            original.Length != patched.Length)
        {
            detail = "Data patch di profil rusak.";
            return PatchState.Unreadable;
        }

        var now = memory.Read(address, original.Length);
        if (now is null)
        {
            detail = "Kode di lokasi itu belum terbaca.";
            return PatchState.Unreadable;
        }

        if (now.AsSpan().SequenceEqual(original)) return PatchState.Original;
        if (now.AsSpan().SequenceEqual(patched)) return PatchState.Patched;

        detail = $"Byte di lokasi kode berbeda (ada {CodePatch.ToHex(now)}, seharusnya {CodePatch.ToHex(original)}). " +
                 "Game kemungkinan sudah di-update.";
        return PatchState.Mismatch;
    }
}