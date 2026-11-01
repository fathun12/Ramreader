using System.Diagnostics;
using System.Runtime.InteropServices;
using Ramreader.Core.Models;

namespace Ramreader.Core.Memory;

/// <summary>
/// Pembaca memori read-only untuk satu proses game.
/// Hanya meminta izin VM_READ, tidak pernah menulis ke game.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    private IntPtr _handle;
    private readonly Process _process;
    private readonly List<ModuleInfo> _modules = new();

    public int ProcessId { get; }
    public string ProcessName { get; }
    public IReadOnlyList<ModuleInfo> Modules => _modules;

    /// <summary>Modul utama (exe game). Dipakai sebagai acuan alamat statis.</summary>
    public ModuleInfo? MainModule => _modules.Count > 0 ? _modules[0] : null;

    /// <summary>False kalau game sudah ditutup.</summary>
    public bool IsAlive
    {
        get
        {
            try { _process.Refresh(); return !_process.HasExited; }
            catch { return false; }
        }
    }

    private ProcessMemory(IntPtr handle, Process process)
    {
        _handle = handle;
        _process = process;
        ProcessId = process.Id;
        ProcessName = process.ProcessName;
    }

    /// <summary>Membuka proses untuk dibaca. Melempar InvalidOperationException dengan pesan yang ramah.</summary>
    public static ProcessMemory Attach(int processId)
    {
        Process process;
        try { process = Process.GetProcessById(processId); }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Proses tidak ditemukan. Mungkin game sudah ditutup.");
        }

        var handle = NativeMethods.OpenProcess(
            NativeMethods.ProcessVmRead | NativeMethods.ProcessQueryInformation, false, processId);

        if (handle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            process.Dispose();
            throw new InvalidOperationException(error == 5
                ? "Akses ditolak. Jalankan Ramreader sebagai Administrator, atau game ini dilindungi anti-cheat."
                : $"Gagal membuka proses (kode error Windows {error}).");
        }

        var memory = new ProcessMemory(handle, process);
        memory.RefreshModules();
        return memory;
    }

    /// <summary>Memuat ulang daftar modul. Panggil lagi kalau game memuat DLL baru.</summary>
    public void RefreshModules()
    {
        _modules.Clear();
        try
        {
            _process.Refresh();
            foreach (ProcessModule module in _process.Modules)
            {
                _modules.Add(new ModuleInfo(
                    module.ModuleName,
                    (ulong)module.BaseAddress.ToInt64(),
                    (ulong)module.ModuleMemorySize));
            }
        }
        catch
        {
            // Beberapa proses terproteksi menolak daftar modul. Biarkan kosong.
        }
    }

    /// <summary>Baca sejumlah byte. Mengembalikan null kalau gagal (alamat tidak valid / tidak terbaca).</summary>
    public byte[]? Read(ulong address, int size)
    {
        if (_handle == IntPtr.Zero || size <= 0) return null;

        var buffer = new byte[size];
        bool ok = NativeMethods.ReadProcessMemory(_handle, (nuint)address, buffer, (nuint)size, out nuint read);
        return ok && (int)read == size ? buffer : null;
    }

    /// <summary>Baca satu nilai bertipe sederhana (int, float, long, ulong, dst).</summary>
    public bool TryRead<T>(ulong address, out T value) where T : unmanaged
    {
        var bytes = Read(address, Marshal.SizeOf<T>());
        if (bytes is null)
        {
            value = default;
            return false;
        }
        value = MemoryMarshal.Read<T>(bytes);
        return true;
    }

    /// <summary>Baca pointer 64-bit. Null kalau gagal.</summary>
    public ulong? ReadPointer(ulong address) =>
        TryRead<ulong>(address, out var value) ? value : null;

    /// <summary>Telusuri semua region memori yang terbaca (opsional hanya yang bisa ditulis = tempat data game).</summary>
    public IEnumerable<MemoryRegion> EnumerateRegions(bool writableOnly)
    {
        uint mask = writableOnly ? NativeMethods.WritableMask : NativeMethods.ReadableMask;
        ulong address = 0;
        nuint size = (nuint)Marshal.SizeOf<NativeMethods.MemoryBasicInformation>();

        while (address < 0x7FFFFFFFFFFF)
        {
            if (NativeMethods.VirtualQueryEx(_handle, (nuint)address, out var info, size) == 0)
                break;

            bool usable = info.State == NativeMethods.MemCommit
                          && (info.Protect & NativeMethods.PageGuard) == 0
                          && (info.Protect & mask) != 0;

            if (usable)
            {
                bool writable = (info.Protect & NativeMethods.WritableMask) != 0;
                yield return new MemoryRegion(info.BaseAddress, info.RegionSize, info.Protect, writable);
            }

            address = info.BaseAddress + info.RegionSize;
        }
    }

    /// <summary>Baca seluruh region secara berpotong-potong supaya hemat RAM. Dasar untuk semua scanner.</summary>
    public IEnumerable<(ulong Address, byte[] Data)> ReadChunks(bool writableOnly, int chunkSize = 16 * 1024 * 1024)
    {
        foreach (var region in EnumerateRegions(writableOnly))
        {
            for (ulong offset = 0; offset < region.Size; offset += (ulong)chunkSize)
            {
                int length = (int)Math.Min((ulong)chunkSize, region.Size - offset);
                var data = Read(region.BaseAddress + offset, length);
                if (data is not null)
                    yield return (region.BaseAddress + offset, data);
            }
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
        _process.Dispose();
    }
}