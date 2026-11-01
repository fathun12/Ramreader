using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ramreader.Trainer.Services;

/// <summary>Satu-satunya kode di seluruh solusi yang menulis ke memori game.</summary>
public sealed class MemoryWriter : IDisposable
{
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmWrite = 0x0020;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint ThreadSuspendResume = 0x0002;

    private IntPtr _handle;
    private readonly int _processId;

    private MemoryWriter(IntPtr handle, int processId)
    {
        _handle = handle;
        _processId = processId;
    }

    /// <summary>Melempar InvalidOperationException (pesan ramah) kalau izin menulis ditolak.</summary>
    public static MemoryWriter Open(int processId)
    {
        var handle = OpenProcess(ProcessVmWrite | ProcessVmOperation, false, processId);
        if (handle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(error == 5
                ? "Izin menulis ditolak. Jalankan Trainer sebagai Administrator, atau game ini dilindungi anti-cheat."
                : $"Gagal membuka izin menulis (kode error Windows {error}).");
        }

        return new MemoryWriter(handle, processId);
    }

    public bool Write(ulong address, byte[] data)
    {
        if (_handle == IntPtr.Zero || data.Length == 0) return false;

        return WriteProcessMemory(_handle, (nuint)address, data, (nuint)data.Length, out nuint written)
               && (int)written == data.Length;
    }

    /// <summary>
    /// Tulis ke KODE game (code patch). Thread game dijeda sebentar supaya tidak ada yang mengeksekusi
    /// byte setengah tertulis; proteksi halaman diubah lalu dikembalikan; cache instruksi dibersihkan.
    /// </summary>
    public bool WriteCode(ulong address, byte[] data, out string error)
    {
        error = "";
        if (_handle == IntPtr.Zero || data.Length == 0)
        {
            error = "Izin menulis belum tersedia.";
            return false;
        }

        var suspended = SuspendAll();
        try
        {
            if (!VirtualProtectEx(_handle, (nuint)address, (nuint)data.Length, PageExecuteReadWrite, out uint oldProtect))
            {
                error = $"Windows menolak mengubah proteksi kode (kode error {Marshal.GetLastWin32Error()}).";
                return false;
            }

            bool ok = WriteProcessMemory(_handle, (nuint)address, data, (nuint)data.Length, out nuint written)
                      && (int)written == data.Length;

            VirtualProtectEx(_handle, (nuint)address, (nuint)data.Length, oldProtect, out _);
            FlushInstructionCache(_handle, (nuint)address, (nuint)data.Length);

            if (!ok) error = "Windows menolak penulisan ke kode game.";
            return ok;
        }
        finally
        {
            ResumeAll(suspended);
        }
    }

    private List<IntPtr> SuspendAll()
    {
        var list = new List<IntPtr>();
        try
        {
            using var process = Process.GetProcessById(_processId);
            foreach (ProcessThread thread in process.Threads)
            {
                var h = OpenThread(ThreadSuspendResume, false, (uint)thread.Id);
                if (h == IntPtr.Zero) continue;

                if (SuspendThread(h) == uint.MaxValue) { CloseHandle(h); continue; }
                list.Add(h);
            }
        }
        catch
        {
            // Proses hilang: tidak ada yang dijeda.
        }
        return list;
    }

    private static void ResumeAll(List<IntPtr> handles)
    {
        foreach (var h in handles)
        {
            ResumeThread(h);
            CloseHandle(h);
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(
        IntPtr process, nuint baseAddress, byte[] buffer, nuint size, out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtectEx(
        IntPtr process, nuint address, nuint size, uint newProtect, out uint oldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushInstructionCache(IntPtr process, nuint baseAddress, nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(uint desiredAccess, bool inheritHandle, uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SuspendThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);
}