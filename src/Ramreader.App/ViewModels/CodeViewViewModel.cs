using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Iced.Intel;
using Ramreader.App.Services;
using Decoder = Iced.Intel.Decoder;

namespace Ramreader.App.ViewModels;

/// <summary>Lihat Kode: membongkar instruksi game di sekitar satu lokasi (hanya membaca memori).</summary>
public partial class CodeViewViewModel : ObservableObject
{
    public GameSession Session { get; }

    [ObservableProperty] private string _offsetText = "97B97F";
    [ObservableProperty] private string _beforeText = "256";
    [ObservableProperty] private string _afterText = "128";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty] private string _output = "";

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool NotConnected => !Session.IsConnected;

    public CodeViewViewModel(GameSession session)
    {
        Session = session;
        Session.PropertyChanged += OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameSession.Memory) or nameof(GameSession.IsConnected))
            OnPropertyChanged(nameof(NotConnected));
    }

    private static bool TryParseOffset(string text, out ulong offset)
    {
        string t = (text ?? "").Trim();
        int plus = t.LastIndexOf('+');
        if (plus >= 0) t = t[(plus + 1)..].Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return ulong.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);
    }

    private static List<Instruction> Decode(byte[] data, int startIndex, ulong startAddress)
    {
        var list = new List<Instruction>();
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(data, startIndex, data.Length - startIndex));
        decoder.IP = startAddress + (ulong)startIndex;
        for (int consumed = 0; consumed < data.Length - startIndex;)
        {
            decoder.Decode(out var instr);
            list.Add(instr); consumed += Math.Max(1, instr.Length);
        }
        return list;
    }

    [RelayCommand]
    private void Show()
    {
        var memory = Session.Memory;
        var module = memory?.MainModule;
        if (memory is null || module is null)
        {
            Message = "Hubungkan ke game dulu di menu Proses.";
            return;
        }

        if (!TryParseOffset(OffsetText, out ulong offset))
        {
            Message = "Offset harus angka hex, mis. 97B97F atau NSUNSC.exe+97B97F.";
            return;
        }

        if (!int.TryParse(BeforeText, out int before)) before = 256;
        if (!int.TryParse(AfterText, out int after)) after = 128;
        before = Math.Clamp(before, 16, 4096);
        after = Math.Clamp(after, 16, 2048);
        if ((ulong)before > offset) before = (int)offset;

        ulong target = module.BaseAddress + offset;
        ulong start = target - (ulong)before;
        var data = memory.Read(start, before + after);
        if (data is null)
        {
            Message = "Memori di sekitar lokasi itu tidak bisa dibaca.";
            return;
        }

        // Cari awal bongkar yang membuat salah satu instruksi jatuh tepat di lokasi.
        int sync = before;
        bool synced = false;
        for (int s = 0; s < 32 && s < before && !synced; s++)
        {
            foreach (var i in Decode(data, s, start))
            {
                if (i.IP == target) { sync = s; synced = true; break; }
            }
        }

        var list = Decode(data, sync, start);
        ulong moduleEnd = module.BaseAddress + (ulong)module.Size;

        string Rva(ulong a) =>
            a >= module.BaseAddress && a < moduleEnd ? $"+{a - module.BaseAddress:X}" : $"0x{a:X}";

        var formatter = new NasmFormatter();
        var sb = new StringBuilder();
        sb.AppendLine($"Lihat Kode: {module.Name}+{offset:X} (dibaca dari memori game yang sedang jalan; kalau patch Trainer menyala, byte patch yang tampil)");
        sb.AppendLine(">> = instruksi di lokasi.  \"; +XXXX\" = tujuan lompat/panggil atau alamat relatif RIP (offset dari exe).");
        if (!synced) sb.AppendLine("PERINGATAN: awal bongkar tidak bisa disejajarkan dengan lokasi; baris di sekitar lokasi mungkin salah potong.");
        sb.AppendLine();

        foreach (var instr in list)
        {
            int idx = (int)(instr.IP - start);
            int len = Math.Max(1, Math.Min(instr.Length, data.Length - idx));
            string bytes = BitConverter.ToString(data, idx, len).Replace('-', ' ');

            string text;
            if (instr.IsInvalid) text = "(tidak terbaca)";
            else
            {
                var output = new StringOutput();
                formatter.Format(instr, output);
                text = output.ToStringAndReset();
            }

            string note = "";
            if (!instr.IsInvalid)
            {
                if (instr.Op0Kind == OpKind.NearBranch64) note = "   ; " + Rva(instr.NearBranch64);
                else if (instr.IsIPRelativeMemoryOperand) note = "   ; [" + Rva(instr.IPRelativeMemoryAddress) + "]";
            }

            string marker = instr.IP == target ? ">>" : "  ";
            sb.AppendLine($"{marker} {module.Name}+{instr.IP - module.BaseAddress:X}  {bytes,-30} {text}{note}");
        }

        Output = sb.ToString();

        try
        {
            System.Windows.Clipboard.SetText(Output);
            Message = "Selesai. Hasil disalin ke clipboard: tempel (Ctrl+V) ke chat.";
        }
        catch (Exception ex)
        {
            Message = "Selesai, tapi gagal menyalin ke clipboard: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Copy()
    {
        if (string.IsNullOrEmpty(Output)) return;
        try
        {
            System.Windows.Clipboard.SetText(Output);
            Message = "Disalin ke clipboard.";
        }
        catch (Exception ex)
        {
            Message = "Gagal menyalin ke clipboard: " + ex.Message;
        }
    }
}