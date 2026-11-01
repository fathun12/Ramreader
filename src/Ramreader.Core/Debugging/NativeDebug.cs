using System.Runtime.InteropServices;

namespace Ramreader.Core.Debugging;

internal static class NativeDebug
{
    public const uint ThreadSuspendResume = 0x0002;
    public const uint ThreadGetContext = 0x0008;
    public const uint ThreadSetContext = 0x0010;

    public const uint DbgContinue = 0x00010002;
    public const uint DbgNotHandled = 0x80010001;

    // CONTEXT_AMD64 | CONTROL | INTEGER | DEBUG_REGISTERS
    public const uint ContextFlags = 0x100000 | 0x1 | 0x2 | 0x10;
    public const int ContextSize = 1232;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DebugActiveProcess(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DebugActiveProcessStop(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DebugSetProcessKillOnExit(bool killOnExit);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WaitForDebugEvent(IntPtr debugEvent, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ContinueDebugEvent(uint processId, uint threadId, uint continueStatus);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenThread(uint desiredAccess, bool inheritHandle, uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SuspendThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetThreadContext(IntPtr thread, IntPtr context);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetThreadContext(IntPtr thread, IntPtr context);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);
}

/// <summary>Offset field di struct CONTEXT x64.</summary>
internal static class Ctx
{
    public const int Flags = 0x30;
    public const int Dr0 = 0x48;
    public const int Dr6 = 0x68;
    public const int Dr7 = 0x70;
    public const int Rax = 0x78;
    public const int Rip = 0xF8;
}

/// <summary>Buffer CONTEXT (harus selaras 16 byte).</summary>
internal sealed class ContextBuffer : IDisposable
{
    private readonly IntPtr _raw;
    public IntPtr Ptr { get; }

    public ContextBuffer()
    {
        _raw = Marshal.AllocHGlobal(NativeDebug.ContextSize + 16);
        Ptr = (IntPtr)(((long)_raw + 15) & ~15L);
    }

    public bool Load(IntPtr thread)
    {
        for (int i = 0; i < NativeDebug.ContextSize; i += 8) Marshal.WriteInt64(Ptr, i, 0);
        Marshal.WriteInt32(Ptr, Ctx.Flags, (int)NativeDebug.ContextFlags);
        return NativeDebug.GetThreadContext(thread, Ptr);
    }

    public bool Store(IntPtr thread) => NativeDebug.SetThreadContext(thread, Ptr);

    public ulong Get(int offset) => (ulong)Marshal.ReadInt64(Ptr, offset);
    public void Set(int offset, ulong value) => Marshal.WriteInt64(Ptr, offset, (long)value);

    /// <summary>Nilai register serba guna (RAX..R15) sesuai urutan di CONTEXT: RAX, RCX, RDX, RBX, RSP, RBP, RSI, RDI, R8..R15.</summary>
    public ulong Gpr(int index) => Get(Ctx.Rax + index * 8);

    public void Dispose() => Marshal.FreeHGlobal(_raw);
}