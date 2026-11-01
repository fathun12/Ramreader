using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Iced.Intel;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Decoder = Iced.Intel.Decoder;

namespace Ramreader.Core.Debugging;

/// <summary>Satu instruksi yang tertangkap menulis ke alamat yang dipantau.</summary>
public sealed class WriterCandidate
{
    public ulong Address { get; init; }
    public string Location { get; init; } = "";
    public string Instruction { get; init; } = "";
    public string BytesHex { get; init; } = "";
    public bool InMainModule { get; init; }
    public bool Verified { get; set; }
    public int Hits { get; set; }
    public int Changes { get; set; }
    public int Decreases { get; set; }

    public WriterCandidate Copy() => (WriterCandidate)MemberwiseClone();
}

/// <summary>
/// "Find out what writes": memasang hardware breakpoint tulis (DR0) di semua thread game lewat debugger Windows,
/// lalu mencatat instruksi yang menulis. Tidak menulis ke memori game. Dilepas penuh saat berhenti.
/// </summary>
public sealed class WriteTracer
{
    private readonly object _lock = new();
    private readonly Dictionary<ulong, WriterCandidate> _candidates = new();
    private readonly InstructionInfoFactory _infoFactory = new();
    private readonly NasmFormatter _formatter = new();

    private ulong _target;
    private int _size;
    private ScanValueType _type;
    private ulong _last;
    private int _totalHits;
    private int _unrecognized;

    public int TotalHits => _totalHits;
    public int Unrecognized => _unrecognized;

    public List<WriterCandidate> Snapshot()
    {
        lock (_lock) return _candidates.Values.Select(c => c.Copy()).ToList();
    }

    /// <summary>
    /// Blokir sampai dibatalkan lewat token, atau game ditutup. Harus dipanggil dari satu thread khusus
    /// (debugger Windows terikat ke thread yang memanggil DebugActiveProcess). Null = berhenti normal.
    /// </summary>
    public string? Run(ProcessMemory memory, ulong target, ScanValueType type, CancellationToken ct)
    {
        _target = target;
        _type = type;
        _size = type is ScanValueType.Int32 or ScanValueType.Float ? 4 : 8;

        if (target % (ulong)_size != 0)
            return "Alamat nilai tidak selaras, jadi tidak bisa dipantau dengan breakpoint perangkat keras.";

        uint pid = (uint)memory.ProcessId;
        ulong dr7 = 1UL | (1UL << 16) | ((_size == 4 ? 3UL : 2UL) << 18);   // DR0 aktif, tulis, panjang 4 atau 8
        _last = ReadRaw(memory, target, _size) ?? 0;

        if (!NativeDebug.DebugActiveProcess(pid))
        {
            int error = Marshal.GetLastWin32Error();
            return error == 5
                ? "Akses ditolak. Jalankan Ramreader sebagai Administrator, atau game ini dilindungi anti-cheat."
                : $"Gagal menempel ke game sebagai debugger (kode error Windows {error}).";
        }

        NativeDebug.DebugSetProcessKillOnExit(false);

        var threads = new Dictionary<uint, IntPtr>();
        using var ctx = new ContextBuffer();
        IntPtr evt = Marshal.AllocHGlobal(256);
        bool gameExited = false;
        bool sawInitialBreak = false;
        string? result = null;

        void Arm(uint tid)
        {
            if (threads.ContainsKey(tid)) return;

            IntPtr h = NativeDebug.OpenThread(
                NativeDebug.ThreadSuspendResume | NativeDebug.ThreadGetContext | NativeDebug.ThreadSetContext, false, tid);
            if (h == IntPtr.Zero) return;

            bool ok = false;
            NativeDebug.SuspendThread(h);
            if (ctx.Load(h))
            {
                ctx.Set(Ctx.Dr0, target);
                ctx.Set(Ctx.Dr6, 0);
                ctx.Set(Ctx.Dr7, dr7);
                ok = ctx.Store(h);
            }
            NativeDebug.ResumeThread(h);

            if (ok) threads[tid] = h;
            else NativeDebug.CloseHandle(h);
        }

        void ArmAll()
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                foreach (System.Diagnostics.ProcessThread t in process.Threads)
                    Arm((uint)t.Id);
            }
            catch
            {
                // Proses hilang: biarkan.
            }
        }

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!NativeDebug.WaitForDebugEvent(evt, 200))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 121) continue;   // timeout, wajar
                    result = $"Pemantauan terhenti (kode error Windows {error}).";
                    break;
                }

                int code = Marshal.ReadInt32(evt, 0);
                uint tid = (uint)Marshal.ReadInt32(evt, 8);
                uint status = NativeDebug.DbgContinue;

                switch (code)
                {
                    case 1: // exception
                    {
                        uint exCode = (uint)Marshal.ReadInt32(evt, 16);
                        if (exCode == 0x80000003)          // breakpoint
                        {
                            if (!sawInitialBreak) { sawInitialBreak = true; ArmAll(); }
                            else status = NativeDebug.DbgNotHandled;
                        }
                        else if (exCode == 0x80000004 && threads.TryGetValue(tid, out var th) && ctx.Load(th))   // single step
                        {
                            ulong dr6 = ctx.Get(Ctx.Dr6);
                            if ((dr6 & 0xF) != 0)
                            {
                                if ((dr6 & 1) != 0) HandleHit(memory, ctx);
                                ctx.Set(Ctx.Dr6, 0);
                                ctx.Store(th);
                            }
                            else status = NativeDebug.DbgNotHandled;
                        }
                        else status = NativeDebug.DbgNotHandled;
                        break;
                    }
                    case 2: // thread baru
                        Arm(tid);
                        break;
                    case 3: // proses dibuat (muncul saat attach)
                    {
                        IntPtr file = Marshal.ReadIntPtr(evt, 16);
                        if (file != IntPtr.Zero) NativeDebug.CloseHandle(file);
                        Arm(tid);
                        break;
                    }
                    case 4: // thread selesai
                        if (threads.Remove(tid, out var gone)) NativeDebug.CloseHandle(gone);
                        break;
                    case 5: // game ditutup
                        gameExited = true;
                        break;
                    case 6: // DLL dimuat
                    {
                        IntPtr file = Marshal.ReadIntPtr(evt, 16);
                        if (file != IntPtr.Zero) NativeDebug.CloseHandle(file);
                        break;
                    }
                }

                NativeDebug.ContinueDebugEvent(pid, tid, status);

                if (gameExited)
                {
                    result = "Game ditutup saat dipantau.";
                    break;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(evt);

            foreach (var h in threads.Values)
            {
                if (!gameExited)
                {
                    NativeDebug.SuspendThread(h);
                    if (ctx.Load(h))
                    {
                        ctx.Set(Ctx.Dr0, 0);
                        ctx.Set(Ctx.Dr6, 0);
                        ctx.Set(Ctx.Dr7, 0);
                        ctx.Store(h);
                    }
                    NativeDebug.ResumeThread(h);
                }
                NativeDebug.CloseHandle(h);
            }

            NativeDebug.DebugActiveProcessStop(pid);
        }

        return result;
    }

    // ---------- hit ----------

    private void HandleHit(ProcessMemory memory, ContextBuffer ctx)
    {
        try
        {
            Interlocked.Increment(ref _totalHits);
            ulong rip = ctx.Get(Ctx.Rip);

            bool changed = false, decreased = false;
            ulong? current = ReadRaw(memory, _target, _size);
            if (current is not null && current.Value != _last)
            {
                changed = true;
                decreased = ToDouble(_type, current.Value) < ToDouble(_type, _last);
                _last = current.Value;
            }

            var code = memory.Read(rip - 15, 15);
            if (code is null) { Interlocked.Increment(ref _unrecognized); return; }

            var found = new List<(ulong Start, Instruction Instr, bool Verified)>();
            for (int len = 1; len <= 15; len++)
            {
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(code, 15 - len, len));
                decoder.IP = rip - (ulong)len;
                var instr = decoder.Decode();
                if (instr.IsInvalid || instr.Length != len) continue;
                if (!WritesMemory(instr)) continue;

                ulong? ea = EffectiveAddress(ctx, instr);
                bool verified = ea is { } a && a <= _target && _target - a < 16;
                found.Add((rip - (ulong)len, instr, verified));
            }

            if (found.Count == 0) { Interlocked.Increment(ref _unrecognized); return; }

            var chosen = found.Where(f => f.Verified).ToList();
            if (chosen.Count == 0) chosen = found;

            lock (_lock)
            {
                foreach (var (start, instr, verified) in chosen)
                {
                    if (!_candidates.TryGetValue(start, out var c))
                    {
                        var module = memory.Modules.FirstOrDefault(
                            m => start >= m.BaseAddress && start < m.BaseAddress + m.Size);
                        var output = new StringOutput();
                        _formatter.Format(instr, output);

                        var bytes = new byte[instr.Length];
                        Array.Copy(code, 15 - instr.Length, bytes, 0, instr.Length);

                        c = new WriterCandidate
                        {
                            Address = start,
                            Location = module is null ? $"0x{start:X}" : $"{module.Name}+{start - module.BaseAddress:X}",
                            Instruction = output.ToStringAndReset(),
                            BytesHex = CodePatch.ToHex(bytes),
                            InMainModule = module is not null && module == memory.MainModule
                        };
                        _candidates[start] = c;
                    }

                    c.Hits++;
                    if (verified) c.Verified = true;
                    if (changed) c.Changes++;
                    if (decreased) c.Decreases++;
                }
            }
        }
        catch
        {
            Interlocked.Increment(ref _unrecognized);
        }
    }

    private bool WritesMemory(Instruction instr)
    {
        var info = _infoFactory.GetInfo(instr);
        foreach (var used in info.GetUsedMemory())
        {
            if (used.Access is OpAccess.Write or OpAccess.CondWrite or OpAccess.ReadWrite or OpAccess.ReadCondWrite)
                return true;
        }
        return false;
    }

    private static ulong? EffectiveAddress(ContextBuffer ctx, Instruction instr)
    {
        if (instr.IsIPRelativeMemoryOperand) return instr.IPRelativeMemoryAddress;

        ulong ea = instr.MemoryDisplacement64;

        if (instr.MemoryBase != Register.None)
        {
            ulong? b = RegValue(ctx, instr.MemoryBase);
            if (b is null) return null;
            ea += b.Value;
        }

        if (instr.MemoryIndex != Register.None)
        {
            ulong? i = RegValue(ctx, instr.MemoryIndex);
            if (i is null) return null;
            ea += i.Value * (ulong)instr.MemoryIndexScale;
        }

        return ea;
    }

    private static ulong? RegValue(ContextBuffer ctx, Register reg)
    {
        var full = reg.GetFullRegister();
        int index = full switch
        {
            Register.RAX => 0, Register.RCX => 1, Register.RDX => 2, Register.RBX => 3,
            Register.RSP => 4, Register.RBP => 5, Register.RSI => 6, Register.RDI => 7,
            Register.R8 => 8, Register.R9 => 9, Register.R10 => 10, Register.R11 => 11,
            Register.R12 => 12, Register.R13 => 13, Register.R14 => 14, Register.R15 => 15,
            _ => -1
        };
        if (index < 0) return null;

        ulong v = ctx.Gpr(index);
        return reg.IsGPR32() ? v & 0xFFFFFFFF : v;
    }

    private static ulong? ReadRaw(ProcessMemory memory, ulong address, int size)
    {
        var bytes = memory.Read(address, size);
        if (bytes is null) return null;
        return size == 4 ? BitConverter.ToUInt32(bytes, 0) : BitConverter.ToUInt64(bytes, 0);
    }

    private static double ToDouble(ScanValueType type, ulong raw) => type switch
    {
        ScanValueType.Int32 => (int)(uint)raw,
        ScanValueType.Float => BitConverter.UInt32BitsToSingle((uint)raw),
        ScanValueType.Int64 => (long)raw,
        _ => BitConverter.UInt64BitsToDouble(raw)
    };
}