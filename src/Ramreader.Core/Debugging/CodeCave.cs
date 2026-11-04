using Iced.Intel;
using Decoder = Iced.Intel.Decoder;

namespace Ramreader.Core.Debugging;

/// <summary>Hasil analisis instruksi penulis: register basis dan displacement alamat tulisnya (mis. [rcx+10h]).</summary>
public sealed record CaveInfo(int BaseReg, long Disp, int Length, int WriterLength = 0, int IndexReg = -1, int Scale = 1);

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

        if (ins.IsInvalid || ins.Length > original.Length)
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
        if (ins.MemoryIndex != Register.None && (ins.MemoryIndex < Register.RAX || ins.MemoryIndex > Register.R15 || ins.MemoryIndex == Register.RSP || ins.MemoryBase == Register.RSP))
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

        int writerLength = ins.Length;
        int pos = writerLength;
        while (pos < original.Length)
        {
            var next = decoder.Decode();
            bool last = pos + next.Length == original.Length;
            if (next.IsInvalid || pos + next.Length > original.Length)
            {
                error = "Byte patch memuat potongan instruksi sesudah penulis.";
                return false;
            }
            if (next.IsIPRelativeMemoryOperand ||
                !(next.FlowControl == FlowControl.Next || (last && next.FlowControl == FlowControl.Return)))
            {
                error = "Instruksi sesudah penulis (digabung karena penulisnya kurang dari 5 byte) cabang/call atau memakai alamat relatif RIP, tidak bisa disalin ke cave.";
                return false;
            }
            pos += next.Length;
        }

        info = new CaveInfo(b - Register.RAX, unchecked((long)ins.MemoryDisplacement64), original.Length, writerLength,
            ins.MemoryIndex == Register.None ? -1 : ins.MemoryIndex - Register.RAX, ins.MemoryIndexScale);
        return true;
    }

    /// <summary>Isi cave. Slot (8 byte) ada di slotOffset; isinya harus ditulis pemanggil.</summary>
    public static byte[] Build(byte[] original, CaveInfo info, ulong caveAddress, ulong instructionAddress, out int slotOffset)
    {
        int n = original.Length;
        ulong ret = instructionAddress + (ulong)n;
        if (info.IndexReg >= 0) return BuildIndexed(original, info, caveAddress, instructionAddress, out slotOffset);
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

    /// <summary>
    /// Cave "pohon": register basis dibandingkan dengan slotCount slot (8 byte). Sama dengan salah satu = satu sisi dengan pemain
    /// (penulisan dilewati); tidak ada yang sama = musuh (instruksi asli jalan). Slot di slotsOffset (diisi pemanggil).
    /// Blok data (24 byte) di dataOffset: +0 dilewati, +4 dijalankan, +8 objek terakhir dilewati, +16 objek terakhir dijalankan.
    /// </summary>
    public static byte[] BuildTreeDiag(byte[] original, CaveInfo info, int slotCount,
        ulong caveAddress, ulong instructionAddress, out int slotsOffset, out int dataOffset)
    {
        int n = original.Length;
        ulong ret = instructionAddress + (ulong)n;
        int reg = info.BaseReg;
        var code = new List<byte>(256);
        var fixups = new List<(int Pos, bool IsSlot, int Index)>();
        var jes = new List<int>();

        void RipRef(bool isSlot, int index)
        {
            fixups.Add((code.Count, isSlot, index));
            code.AddRange(new byte[4]);
        }

        code.Add(0x9C);                                        // pushfq
        for (int i = 0; i < slotCount; i++)
        {
            code.Add((byte)(0x48 | (reg >= 8 ? 4 : 0)));       // cmp reg,[rip+slot_i]
            code.Add(0x3B);
            code.Add((byte)(0x05 | ((reg & 7) << 3)));
            RipRef(true, i);
            code.Add(0x0F);                                    // je skip (rel32)
            code.Add(0x84);
            jes.Add(code.Count);
            code.AddRange(new byte[4]);
        }

        code.Add(0xFF);                                        // dijalankan++
        code.Add(0x05);
        RipRef(false, 4);
        code.Add((byte)(0x48 | (reg >= 8 ? 4 : 0)));           // objek terakhir dijalankan
        code.Add(0x89);
        code.Add((byte)(0x05 | ((reg & 7) << 3)));
        RipRef(false, 16);
        code.Add(0x9D);                                        // popfq
        code.AddRange(original);
        int j2 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)j2 + 5)));

        int skipStart = code.Count;
        code.Add(0xFF);                                        // dilewati++
        code.Add(0x05);
        RipRef(false, 0);
        code.Add((byte)(0x48 | (reg >= 8 ? 4 : 0)));           // objek terakhir dilewati
        code.Add(0x89);
        code.Add((byte)(0x05 | ((reg & 7) << 3)));
        RipRef(false, 8);
        code.Add(0x9D);                                        // popfq
        int j1 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)j1 + 5)));

        slotsOffset = (code.Count + 7) & ~7;
        dataOffset = slotsOffset + slotCount * 8;
        while (code.Count < dataOffset + 24) code.Add(0);

        var bytes = code.ToArray();
        foreach (int at in jes)
            BitConverter.GetBytes(skipStart - (at + 4)).CopyTo(bytes, at);
        foreach (var (pos, isSlot, index) in fixups)
        {
            int target = isSlot ? slotsOffset + index * 8 : dataOffset + index;
            BitConverter.GetBytes(target - (pos + 4)).CopyTo(bytes, pos);
        }
        return bytes;
    }

    /// <summary>Offset di cave tempat "ekor" ditaruh bila penulis kurang dari 5 byte (cave 0x1000 byte, blok utama jauh lebih kecil).</summary>
    public const int TailOffset = 0x800;

    /// <summary>
    /// Cave "pemilik efek" untuk hook call (E8 rel32, 5 byte): panggil target asli; kalau hasilnya 0 dan [rdi+18h] sama dengan
    /// salah satu slot (pemilik efek milik sisi pemain), hasilnya dipaksa 1. Hanya rax yang disentuh.
    /// Blok data (24 byte) di dataOffset: +0 dipaksa 1, +4 dibiarkan, +8 pemilik terakhir dipaksa, +16 pemilik terakhir dibiarkan.
    /// </summary>
    public static byte[] BuildCallOwner(ulong callTarget, int slotCount, ulong caveAddress, ulong instructionAddress,
        out int slotsOffset, out int dataOffset)
    {
        ulong ret = instructionAddress + 5;
        var code = new List<byte>(256);
        var fixups = new List<(int Pos, bool IsSlot, int Index)>();
        var jes = new List<int>();

        void RipRef(bool isSlot, int index)
        {
            fixups.Add((code.Count, isSlot, index));
            code.AddRange(new byte[4]);
        }

        code.Add(0xE8);                                        // call target asli
        code.AddRange(BitConverter.GetBytes(Rel(callTarget, caveAddress + 5)));
        code.Add(0x85); code.Add(0xC0);                        // test eax,eax
        code.Add(0x0F); code.Add(0x85);                        // jne selesai (hasil sudah tidak nol)
        int jneAt = code.Count;
        code.AddRange(new byte[4]);
        code.Add(0x48); code.Add(0x8B); code.Add(0x47); code.Add(0x18);   // mov rax,[rdi+18h]
        for (int i = 0; i < slotCount; i++)
        {
            code.Add(0x48); code.Add(0x3B); code.Add(0x05);    // cmp rax,[rip+slot_i]
            RipRef(true, i);
            code.Add(0x0F); code.Add(0x84);                    // je cocok
            jes.Add(code.Count);
            code.AddRange(new byte[4]);
        }
        code.Add(0xFF); code.Add(0x05);                        // dibiarkan++
        RipRef(false, 4);
        code.Add(0x48); code.Add(0x89); code.Add(0x05);        // pemilik terakhir dibiarkan
        RipRef(false, 16);
        code.Add(0x31); code.Add(0xC0);                        // xor eax,eax (hasil asli 0)
        code.Add(0xE9);                                        // jmp selesai
        int jmpDoneAt = code.Count;
        code.AddRange(new byte[4]);

        int matchStart = code.Count;
        code.Add(0xFF); code.Add(0x05);                        // dipaksa++
        RipRef(false, 0);
        code.Add(0x48); code.Add(0x89); code.Add(0x05);        // pemilik terakhir dipaksa
        RipRef(false, 8);
        code.Add(0xB8);                                        // mov eax,1
        code.AddRange(BitConverter.GetBytes(1));

        int doneStart = code.Count;
        code.Add(0xE9);                                        // jmp balik ke game (sesudah call)
        int jmpRetAt = code.Count;
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)jmpRetAt + 4)));

        slotsOffset = (code.Count + 7) & ~7;
        dataOffset = slotsOffset + slotCount * 8;
        while (code.Count < dataOffset + 24) code.Add(0);

        var bytes = code.ToArray();
        BitConverter.GetBytes(doneStart - (jneAt + 4)).CopyTo(bytes, jneAt);
        BitConverter.GetBytes(doneStart - (jmpDoneAt + 4)).CopyTo(bytes, jmpDoneAt);
        foreach (int at in jes)
            BitConverter.GetBytes(matchStart - (at + 4)).CopyTo(bytes, at);
        foreach (var (pos, isSlot, index) in fixups)
        {
            int target = isSlot ? slotsOffset + index * 8 : dataOffset + index;
            BitConverter.GetBytes(target - (pos + 4)).CopyTo(bytes, pos);
        }
        return bytes;
    }

    /// <summary>
    /// Ekor cave untuk patch gabungan: salinan instruksi sesudah penulis (ikut tertimpa lompatan 5 byte),
    /// lalu lompat ke game sesudah semuanya (kecuali ekornya berakhir ret).
    /// </summary>
    public static byte[] BuildTail(byte[] original, CaveInfo info, ulong tailAddress, ulong instructionAddress)
    {
        var tail = original[info.WriterLength..];
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(tail));
        decoder.IP = 0x1000;
        bool endsWithRet = false;
        int pos = 0;
        while (pos < tail.Length)
        {
            var ins = decoder.Decode();
            if (ins.IsInvalid) break;
            endsWithRet = ins.FlowControl == FlowControl.Return;
            pos += ins.Length;
        }

        var code = new List<byte>(tail);
        if (!endsWithRet)
        {
            int at = code.Count;
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(Rel(instructionAddress + (ulong)original.Length, tailAddress + (ulong)at + 5)));
        }
        return code.ToArray();
    }

    private static int Rel(ulong target, ulong next) => checked((int)((long)target - (long)next));

    /// <summary>Cave pemain-saja untuk alamat tulis berindeks [base+index*scale+disp]: bandingkan base+index*scale (lewat r11) dengan slot.</summary>
    private static byte[] BuildIndexed(byte[] original, CaveInfo info, ulong caveAddress, ulong instructionAddress, out int slotOffset)
    {
        int n = original.Length;
        ulong ret = instructionAddress + (ulong)n;
        int bas = info.BaseReg, idx = info.IndexReg;
        int scaleBits = info.Scale switch { 2 => 1, 4 => 2, 8 => 3, _ => 0 };
        var code = new List<byte>(64);

        code.Add(0x9C);                                        // pushfq
        code.Add(0x41); code.Add(0x53);                        // push r11
        code.Add((byte)(0x4C | (idx >= 8 ? 2 : 0) | (bas >= 8 ? 1 : 0)));
        code.Add(0x8D);                                        // lea r11,[base+index*scale]
        bool disp8 = (bas & 7) == 5;
        code.Add((byte)(disp8 ? 0x5C : 0x1C));
        code.Add((byte)((scaleBits << 6) | ((idx & 7) << 3) | (bas & 7)));
        if (disp8) code.Add(0);
        code.Add(0x4C); code.Add(0x3B); code.Add(0x1D);        // cmp r11,[rip+slot]
        int fix = code.Count;
        code.AddRange(new byte[4]);
        code.Add(0x41); code.Add(0x5B);                        // pop r11 (flag tidak berubah)
        code.Add(0x75); code.Add(0x06);                        // jne -> instruksi asli
        code.Add(0x9D);                                        // popfq (pemain: lewati penulisan)
        int j1 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)j1 + 5)));
        code.Add(0x9D);                                        // popfq (bukan pemain)
        code.AddRange(original);
        int j2 = code.Count;
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(Rel(ret, caveAddress + (ulong)j2 + 5)));

        slotOffset = (code.Count + 7) & ~7;
        while (code.Count < slotOffset + 8) code.Add(0);
        var bytes = code.ToArray();
        BitConverter.GetBytes(slotOffset - (fix + 4)).CopyTo(bytes, fix);
        return bytes;
    }
}