using System.Runtime.InteropServices;
using Iced.Intel;
using Ramreader.Core.Memory;
using Decoder = Iced.Intel.Decoder;

namespace Ramreader.Core.Debugging;

/// <summary>Satu objek (basis register) yang tertangkap saat instruksi dijalankan.</summary>
public sealed class ObjectEntry
{
    public ulong Base { get; init; }
    public int Hits { get; set; }

    public ObjectEntry Copy() => (ObjectEntry)MemberwiseClone();
}

/// <summary>Hasil analisis instruksi: register basis (0=RAX..15=R15) dan displacement alamat tulisnya.</summary>
public sealed record TraceTarget(int BaseReg, long Disp, string Instruction);

/// <summary>
/// Memasang hardware breakpoint EKSEKUSI (DR0) pada satu instruksi di semua thread game, lalu mencatat
/// nilai register basisnya tiap kali instruksi itu jalan. Hasilnya daftar objek karakter yang memakai instruksi itu.
/// Tidak menulis ke memori game. Dilepas penuh saat berhenti.
/// </summary>
public sealed class ObjectTracer
{
    private const int MaxObjects = 200;
    private const int EFlagsOffset = 0x44;

    private readonly object _lock = new();
    private readonly Dictionary<ulong, ObjectEntry> _objects = new();
    private int _totalHits;

    public int TotalHits => _totalHits;

    public List<ObjectEntry> Snapshot()
    {
        lock (_lock) return _objects.Values.Select(o => o.Copy()).ToList();
    }

    public static TraceTarget? Analyze(ProcessMemory memory, ulong address, out string error)
    {
        error = "";
        var head = memory.Read(address, 15);
        if (head is null)
        {
            error = "Gagal membaca kode di alamat instruksi itu.";
            return null;
        }

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(head));
        decoder.IP = address;
        var ins = decoder.Decode();
        if (ins.IsInvalid)
        {
            error = "Byte di alamat itu bukan instruksi yang valid. Cek offset yang diketik.";
            return null;
        }

        var b = ins.MemoryBase;
        if (b < Register.RAX || b > Register.R15)
        {
            error = "Instruksi di alamat itu tidak memakai register 64-bit sebagai basis alamat " +
                    "(kalau Trainer sedang menyalakan patch di sini, matikan dulu).";
            return null;
        }

        var output = new StringOutput();
        new NasmFormatter().Format(ins, output);
        return new TraceTarget(b - Register.RAX, unchecked((long)ins.MemoryDisplacement64), output.ToStringAndReset());
    }

    /// <summary>Blokir sampai dibatalkan atau game ditutup. Panggil dari satu thread khusus. Null = berhenti normal.</summary>
    public string? Run(ProcessMemory memory, ulong instructionAddress, int baseRegister, CancellationToken ct)
    {
        uint pid = (uint)memory.ProcessId;
        const ulong dr7 = 1UL;   // DR0 aktif, jenis eksekusi (RW=00, LEN=00)

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
                ctx.Set(Ctx.Dr0, instructionAddress);
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
                                if ((dr6 & 1) != 0) HandleHit(ctx, baseRegister);
                                ctx.Set(Ctx.Dr6, 0);
                                // Resume Flag: supaya instruksi yang sama tidak memicu breakpoint lagi tanpa akhir.
                                Marshal.WriteInt32(ctx.Ptr, EFlagsOffset, Marshal.ReadInt32(ctx.Ptr, EFlagsOffset) | 0x10000);
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

    private void HandleHit(ContextBuffer ctx, int baseRegister)
    {
        Interlocked.Increment(ref _totalHits);

        ulong b = ctx.Gpr(baseRegister);
        if (b < 0x10000) return;

        lock (_lock)
        {
            if (!_objects.TryGetValue(b, out var o))
            {
                if (_objects.Count >= MaxObjects) return;
                o = new ObjectEntry { Base = b };
                _objects[b] = o;
            }
            o.Hits++;
        }
    }
}