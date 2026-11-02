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

    /// <summary>
    /// Cave "tim": cmp dword [reg+markerOffset], imm32. Kalau sama dengan penanda pemain (satu tim), penulisan dilewati;
    /// kalau beda (musuh atau objek lain), instruksi asli dijalankan. Imm32 ada di immOffset dan diperbarui Trainer.
    /// </summary>
    public static byte[] BuildTeam(byte[] original, CaveInfo info, int markerOffset, uint marker,
        ulong caveAddress, ulong instructionAddress, out int immOffset)
    {
        int n = original.Length;
        ulong ret = instructionAddress + (ulong)n;
        int reg = info.BaseReg;
        var code = new List<byte>(64);

        code.Add(0x9C);                                        // pushfq
        if (reg >= 8) code.Add(0x41);                          // REX.B
        code.Add(0x81);                                        // cmp dword [reg+disp32], imm32
        code.Add((byte)(0xB8 | (reg & 7)));                    // mod=10, /7, rm=reg
        if ((reg & 7) == 4) code.Add(0x24);                    // SIB untuk rsp/r12
        code.AddRange(BitConverter.GetBytes(markerOffset));
        immOffset = code.Count;
        code.AddRange(BitConverter.GetBytes(marker));
        code.Add(0x75);                                        // jne -> instruksi asli
        code.Add(0x06);
        code.Add(0x9D);                                        // popfq (satu tim: lewati penulisan)
        int jmp1 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)jmp1 + 5)));
        code.Add(0x9D);                                        // popfq (bukan satu tim)
        code.AddRange(original);
        int jmp2 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)jmp2 + 5)));

        return code.ToArray();
    }

    /// <summary>
    /// Cave "tim" dengan penghitung diagnostik. Blok data (24 byte) ada di dataOffset:
    /// +0 jumlah dilewati, +4 jumlah dijalankan, +8 objek terakhir dilewati, +16 objek terakhir dijalankan.
    /// </summary>
    public static byte[] BuildTeamDiag(byte[] original, CaveInfo info, int markerOffset, uint marker,
        ulong caveAddress, ulong instructionAddress, out int immOffset, out int dataOffset)
    {
        int n = original.Length;
        ulong ret = instructionAddress + (ulong)n;
        int reg = info.BaseReg;
        var code = new List<byte>(128);
        var fixups = new List<(int Pos, int Slot)>();

        void RipRef(int slot)
        {
            fixups.Add((code.Count, slot));
            code.AddRange(new byte[4]);
        }
        void IncDword(int slot)
        {
            code.Add(0xFF);
            code.Add(0x05);
            RipRef(slot);
        }
        void StoreReg(int slot)
        {
            code.Add((byte)(0x48 | (reg >= 8 ? 4 : 0)));
            code.Add(0x89);
            code.Add((byte)(0x05 | ((reg & 7) << 3)));
            RipRef(slot);
        }

        code.Add(0x9C);                                        // pushfq
        if (reg >= 8) code.Add(0x41);
        code.Add(0x81);                                        // cmp dword [reg+disp32], imm32
        code.Add((byte)(0xB8 | (reg & 7)));
        if ((reg & 7) == 4) code.Add(0x24);
        code.AddRange(BitConverter.GetBytes(markerOffset));
        immOffset = code.Count;
        code.AddRange(BitConverter.GetBytes(marker));
        code.Add(0x75);                                        // jne -> jalan
        int jneAt = code.Count;
        code.Add(0);
        int skipStart = code.Count;

        IncDword(0);                                           // dilewati++
        StoreReg(8);                                           // objek terakhir dilewati
        code.Add(0x9D);                                        // popfq
        int j1 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)j1 + 5)));
        code[jneAt] = (byte)(code.Count - skipStart);

        IncDword(4);                                           // jalan++
        StoreReg(16);                                          // objek terakhir jalan
        code.Add(0x9D);                                        // popfq
        code.AddRange(original);
        int j2 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)j2 + 5)));

        dataOffset = (code.Count + 7) & ~7;
        while (code.Count < dataOffset + 24) code.Add(0);

        var bytes = code.ToArray();
        foreach (var (pos, slot) in fixups)
            BitConverter.GetBytes((dataOffset + slot) - (pos + 4)).CopyTo(bytes, pos);

        return bytes;
    }

    /// <summary>
    /// Cave tim: bandingkan beberapa dword berurutan di objek ([reg+firstOffset], +4, +8, ...) dengan penanda pemain.
    /// Semua sama = satu tim (penulisan dilewati); ada yang beda = musuh (instruksi asli jalan).
    /// Blok data (24 byte) di dataOffset: +0 dilewati, +4 dijalankan, +8 objek terakhir dilewati, +16 objek terakhir dijalankan.
    /// </summary>
    public static byte[] BuildTeamDiagMulti(byte[] original, CaveInfo info, int firstOffset, uint[] markers,
        ulong caveAddress, ulong instructionAddress, out int[] immOffsets, out int dataOffset)
    {
        int n = original.Length;
        ulong ret = instructionAddress + (ulong)n;
        int reg = info.BaseReg;
        var code = new List<byte>(160);
        var fixups = new List<(int Pos, int Slot)>();
        var jnes = new List<int>();
        immOffsets = new int[markers.Length];

        void RipRef(int slot)
        {
            fixups.Add((code.Count, slot));
            code.AddRange(new byte[4]);
        }
        void IncDword(int slot)
        {
            code.Add(0xFF);
            code.Add(0x05);
            RipRef(slot);
        }
        void StoreReg(int slot)
        {
            code.Add((byte)(0x48 | (reg >= 8 ? 4 : 0)));
            code.Add(0x89);
            code.Add((byte)(0x05 | ((reg & 7) << 3)));
            RipRef(slot);
        }

        code.Add(0x9C);                                        // pushfq
        for (int i = 0; i < markers.Length; i++)
        {
            if (reg >= 8) code.Add(0x41);                      // cmp dword [reg+disp32], imm32
            code.Add(0x81);
            code.Add((byte)(0xB8 | (reg & 7)));
            if ((reg & 7) == 4) code.Add(0x24);
            code.AddRange(BitConverter.GetBytes(firstOffset + i * 4));
            immOffsets[i] = code.Count;
            code.AddRange(BitConverter.GetBytes(markers[i]));
            code.Add(0x75);                                    // jne -> jalan (musuh)
            jnes.Add(code.Count);
            code.Add(0);
        }

        IncDword(0);                                           // satu tim: dilewati++
        StoreReg(8);
        code.Add(0x9D);                                        // popfq
        int j1 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)j1 + 5)));

        int runStart = code.Count;
        foreach (int at in jnes) code[at] = (byte)(runStart - (at + 1));

        IncDword(4);                                           // musuh: dijalankan++
        StoreReg(16);
        code.Add(0x9D);                                        // popfq
        code.AddRange(original);
        int j2 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)j2 + 5)));

        dataOffset = (code.Count + 7) & ~7;
        while (code.Count < dataOffset + 24) code.Add(0);

        var bytes = code.ToArray();
        foreach (var (pos, slot) in fixups)
            BitConverter.GetBytes((dataOffset + slot) - (pos + 4)).CopyTo(bytes, pos);

        return bytes;
    }

    private static int Rel(ulong target, ulong next) => checked((int)((long)target - (long)next));
}