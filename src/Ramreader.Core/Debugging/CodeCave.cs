using Iced.Intel;
using Decoder = Iced.Intel.Decoder;

namespace Ramreader.Core.Debugging;

/// <summary>Hasil analisis instruksi penulis: register basis dan displacement alamat tulisnya (mis. [rcx+10h]).</summary>
public sealed record CaveInfo(int BaseReg, long Disp, int Length);

/// <summary>
/// Code cave "pemain saja": instruksi penulis diganti jmp ke cave. Di cave, kalau register basis sama dengan
/// nilai di slot (alamat karakter pemain), penulisan dilewati; kalau beda (musuh), instruksi asli dijalankan.
/// Flag disimpan dengan pushfq/popfq karena kode setelahnya sering memakai flag dari perbandingan sebelumnya.
/// </summary>
public static class CodeCave
{
    public static bool TryAnalyze(byte[] original, out CaveInfo? info, out string error)
    {
        info = null;
        error = "";

        if (original.Length < 5)
        {
            error = "Instruksi lebih pendek dari 5 byte, tidak muat untuk lompatan.";
            return false;
        }

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(original));
        decoder.IP = 0x1000;
        var ins = decoder.Decode();

        if (ins.IsInvalid || ins.Length != original.Length)
        {
            error = "Byte patch bukan satu instruksi utuh.";
            return false;
        }
        if (ins.FlowControl != FlowControl.Next)
        {
            error = "Instruksi ini cabang/call.";
            return false;
        }
        if (ins.IsIPRelativeMemoryOperand)
        {
            error = "Instruksi memakai alamat relatif RIP.";
            return false;
        }
        if (ins.MemoryIndex != Register.None)
        {
            error = "Instruksi memakai register indeks.";
            return false;
        }

        var b = ins.MemoryBase;
        if (b < Register.RAX || b > Register.R15)
        {
            error = "Instruksi tidak memakai register 64-bit sebagai basis alamat.";
            return false;
        }

        info = new CaveInfo(b - Register.RAX, unchecked((long)ins.MemoryDisplacement64), original.Length);
        return true;
    }

    /// <summary>Isi cave. Slot (8 byte) ada di slotOffset; isinya harus ditulis pemanggil.</summary>
    public static byte[] Build(byte[] original, CaveInfo info, ulong caveAddress, ulong instructionAddress, out int slotOffset)
    {
        int n = original.Length;
        ulong ret = instructionAddress + (ulong)n;
        slotOffset = (22 + n + 7) & ~7;

        var code = new byte[slotOffset + 8];
        int reg = info.BaseReg;

        code[0] = 0x9C;                                        // pushfq
        code[1] = (byte)(0x48 | (reg >= 8 ? 4 : 0));           // cmp reg,[rip+slot]
        code[2] = 0x3B;
        code[3] = (byte)(0x05 | ((reg & 7) << 3));
        BitConverter.GetBytes(slotOffset - 8).CopyTo(code, 4);
        code[8] = 0x75;                                        // jne -> instruksi asli
        code[9] = 0x06;
        code[10] = 0x9D;                                       // popfq (pemain: lewati penulisan)
        code[11] = 0xE9;
        BitConverter.GetBytes(Rel(ret, caveAddress + 16)).CopyTo(code, 12);
        code[16] = 0x9D;                                       // popfq (bukan pemain)
        Array.Copy(original, 0, code, 17, n);
        code[17 + n] = 0xE9;
        BitConverter.GetBytes(Rel(ret, caveAddress + 22 + (ulong)n)).CopyTo(code, 18 + n);

        return code;
    }

    private static int Rel(ulong target, ulong next) => checked((int)((long)target - (long)next));
}