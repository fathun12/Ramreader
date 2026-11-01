using System.Runtime.InteropServices;

namespace Ramreader.Core.Memory;

internal static class NativeMethods
{
    public const uint ProcessVmRead = 0x0010;
    public const uint ProcessQueryInformation = 0x0400;

    public const uint MemCommit = 0x1000;
    public const uint PageGuard = 0x100;

    // PAGE_READONLY | READWRITE | WRITECOPY | EXECUTE_READ | EXECUTE_READWRITE | EXECUTE_WRITECOPY
    public const uint ReadableMask = 0x02 | 0x04 | 0x08 | 0x20 | 0x40 | 0x80;
    // PAGE_READWRITE | WRITECOPY | EXECUTE_READWRITE | EXECUTE_WRITECOPY
    public const uint WritableMask = 0x04 | 0x08 | 0x40 | 0x80;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReadProcessMemory(
        IntPtr process, nuint baseAddress, byte[] buffer, nuint size, out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nuint VirtualQueryEx(
        IntPtr process, nuint address, out MemoryBasicInformation info, nuint length);

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryBasicInformation
    {
        public ulong BaseAddress;
        public ulong AllocationBase;
        public uint AllocationProtect;
        public uint Alignment1;
        public ulong RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
        public uint Alignment2;
    }
}