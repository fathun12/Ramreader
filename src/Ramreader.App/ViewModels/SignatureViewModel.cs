  using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ramreader.App.Services;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Core.Pointers;
using Ramreader.Core.Scanning;

namespace Ramreader.App.ViewModels;

/// <summary>Satu baris kandidat: tempat di kode game yang memakai alamat statis jalur.</summary>
public sealed class RefRow
{
    public RefRow(RipReference reference, string location, string bytes)
    {
        Reference = reference;
        LocationText = location;
        BytesText = bytes;
    }

    public RipReference Reference { get; }
    public string LocationText { get; }
    public string BytesText { get; }
    public string DeltaText => Reference.Delta == 0 ? "tepat" : $"selisih 0x{Reference.Delta:X}";
}

public partial class SignatureViewModel : ObservableObject
{
    private const int MaxRows = 300;

    private const string StartHint =
        "Langkah 1: pilih alamat yang sudah kamu simpan (menu Tersimpan), lalu klik Cari Kode Pengguna. " +
        "Ramreader mencari bagian kode game yang memakai alamat statis jalur itu, jadi tidak perlu Cheat Engine.";

    private const string AfterFindHint =
        "Langkah 2: pilih satu baris (yang selisihnya \"tepat\" paling pas), lalu klik Buat Signature.";

    private const string AfterMakeHint =
        "Langkah 3: klik Uji Pola, lalu Simpan ke Alamat. Sesudah itu klik Tes Signature (juga setelah restart game) " +
        "untuk membuktikan signature menemukan alamat yang sama.";

    private CancellationTokenSource? _cts;

    // Informasi RIP untuk pola yang sedang ada di kotak Pola (hasil Buat Signature).
    private int _dispOffset = -1;
    private int _instructionEnd;
    private ulong _staticDelta;
    private int _madeLength;

    public GameSession Session { get; }
    public SavedViewModel Saved { get; }

    public IReadOnlyList<OffsetOption> DeltaOptions { get; } = new[]
    {
        new OffsetOption(0, "Tepat (0)"),
        new OffsetOption(0x100, "Dekat (sampai 0x100)"),
        new OffsetOption(0x400, "Jauh (sampai 0x400)")
    };

    public ObservableCollection<RefRow> Candidates { get; } = new();

    [ObservableProperty]
    private SavedItem? _selectedSaved;

    [ObservableProperty]
    private OffsetOption _selectedDelta;

    [ObservableProperty]
    private RefRow? _selectedCandidate;

    [ObservableProperty]
    private string _patternText = "";

    [ObservableProperty]
    private string _instructionInput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty]
    private string _hint = StartHint;

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private double _scanProgress;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool NotConnected => !Session.IsConnected;
    public bool ShowNoSaved => Session.IsConnected && Saved.IsEmpty;

    public SignatureViewModel(GameSession session, SavedViewModel saved)
    {
        Session = session;
        Saved = saved;
        _selectedDelta = DeltaOptions[1];

        Session.PropertyChanged += OnSessionChanged;
        Saved.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SavedViewModel.IsEmpty))
                OnPropertyChanged(nameof(ShowNoSaved));
        };
    }

    partial void OnIsBusyChanged(bool value) => UpdateStates();
    partial void OnSelectedSavedChanged(SavedItem? value)
    {
        Candidates.Clear();
        SelectedCandidate = null;
        UpdateStates();
    }
    partial void OnSelectedCandidateChanged(RefRow? value) => UpdateStates();
    partial void OnPatternTextChanged(string value) => UpdateStates();
    partial void OnInstructionInputChanged(string value) => UpdateStates();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(GameSession.Memory) or nameof(GameSession.IsConnected)))
            return;

        OnPropertyChanged(nameof(NotConnected));
        OnPropertyChanged(nameof(ShowNoSaved));

        // Alamat kandidat hanya berlaku untuk satu proses.
        if (e.PropertyName == nameof(GameSession.Memory))
        {
            if (IsBusy) _cts?.Cancel();
            Candidates.Clear();
            SelectedCandidate = null;
            Summary = "";
            Hint = StartHint;
        }

        UpdateStates();
    }

    // ---------- perintah ----------

    private bool CanFind() => Session.IsConnected && !IsBusy && SelectedSaved is not null;
    private bool CanMake() => CanFind() && SelectedCandidate is not null;
    private bool CanMakeFromAddress() => Session.IsConnected && !IsBusy && !string.IsNullOrWhiteSpace(InstructionInput);
    private bool CanTest() => Session.IsConnected && !IsBusy && !string.IsNullOrWhiteSpace(PatternText);
    private bool CanSave() => CanFind() && !string.IsNullOrWhiteSpace(PatternText);
    private bool CanTestSaved() => CanFind() && SelectedSaved is { } s && s.Saved.Signatures.Count > 0;
    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanFind))]
    private async Task FindRefsAsync()
    {
        var memory = Session.Memory;
        var item = SelectedSaved;
        if (memory is null || item is null) return;
        if (!TryGetRoot(memory, item, out var module, out var chain)) return;

        ulong staticAddress = module.BaseAddress + chain.ModuleOffset;
        ulong maxDelta = SelectedDelta.MaxOffset;
        List<RefRow>? rows = null;

        await RunAsync("Mencari kode yang memakai alamat statis jalur utama...", async (progress, ct) =>
        {
            rows = await Task.Run(() =>
            {
                var found = RipReferenceScanner.Find(memory, module, staticAddress, maxDelta, MaxRows, progress, ct);
                var list = new List<RefRow>(found.Count);
                foreach (var reference in found)
                {
                    var bytes = memory.Read(reference.DispAddress - 3, 12);
                    string text = bytes is null ? "?" : string.Join(' ', bytes.Select(b => b.ToString("X2")));
                    list.Add(new RefRow(reference,
                        $"{module.Name}+{reference.DispAddress - module.BaseAddress:X}", text));
                }
                return list;
            }, ct);
        });

        if (rows is null) return;

        Candidates.Clear();
        SelectedCandidate = null;
        foreach (var row in rows) Candidates.Add(row);

        if (rows.Count == 0)
        {
            Summary = "";
            Hint = StartHint;
            Message = "Tidak ada kode yang memakai alamat ini. Coba jarak \"Jauh\", atau pilih alamat tersimpan lain.";
            return;
        }

        Summary = $"{rows.Count:N0} lokasi kode ditemukan  •  alamat statis jalur: {module.Name}+{chain.ModuleOffset:X}";
        Hint = AfterFindHint;
    }

    [RelayCommand(CanExecute = nameof(CanMake))]
    private async Task MakeAsync()
    {
        var memory = Session.Memory;
        var row = SelectedCandidate;
        var module = memory?.MainModule;
        if (memory is null || row is null || module is null) return;

        SignatureResult? result = null;
        await RunAsync("Membuat pola unik (bisa beberapa detik)...", async (progress, ct) =>
        {
            result = await Task.Run(() =>
                SignatureMaker.MakeForReference(memory, module, row.Reference, progress, ct), ct);
        });

        if (result is null) return;
        if (result.Pattern is null)
        {
            Message = result.Message;
            return;
        }

        ApplyResult(result, row.Reference.Delta);
    }

    [RelayCommand(CanExecute = nameof(CanMakeFromAddress))]
    private async Task MakeFromAddressAsync()
    {
        var memory = Session.Memory;
        var module = memory?.MainModule;
        if (memory is null || module is null) return;

        if (!TryParseAddress(InstructionInput, memory, out var address))
        {
            Message = "Alamat tidak valid. Contoh: NSUNSC.exe+1A2B3C4 atau 0x7FF6A1B2C3D4.";
            return;
        }

        SignatureResult? result = null;
        await RunAsync("Membuat pola dari alamat instruksi...", async (progress, ct) =>
        {
            result = await Task.Run(() =>
                SignatureMaker.MakeExact(memory, module, address, progress, ct), ct);
        });

        if (result is null) return;
        if (result.Pattern is null)
        {
            Message = result.Message;
            return;
        }

        ApplyResult(result, 0);
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestAsync()
    {
        var memory = Session.Memory;
        var module = memory?.MainModule;
        if (memory is null || module is null) return;

        if (!AobPattern.TryParse(PatternText, out var pattern, out var error))
        {
            Message = error;
            return;
        }

        List<ulong>? hits = null;
        await RunAsync("Menguji pola di modul utama...", async (progress, ct) =>
        {
            hits = await Task.Run(() => AobScanner.Scan(memory, module, pattern, 20, progress, ct), ct);
        });

        if (hits is null) return;

        Message = hits.Count switch
        {
            0 => "Pola tidak ditemukan di kode game. Cek apakah ada byte yang salah.",
            1 => $"Pola unik: cocok di tepat 1 tempat ({Where(memory, hits[0])}). Siap disimpan.",
            _ => $"Pola cocok di {(hits.Count >= 20 ? "20 atau lebih" : hits.Count.ToString())} tempat, belum unik. " +
                 "Ganti ?? dengan byte yang pasti, atau klik Buat Signature lagi."
        };
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var memory = Session.Memory;
        var item = SelectedSaved;
        if (memory is null || item is null) return;
        if (!TryGetRoot(memory, item, out var module, out var chain)) return;

        if (!AobPattern.TryParse(PatternText, out var pattern, out var error))
        {
            Message = error;
            return;
        }

        if (_dispOffset >= 0 && pattern.Length != _madeLength)
        {
            Message = "Panjang pola berubah sejak Buat Signature, jadi posisi alamat otomatisnya tidak berlaku lagi. " +
                      "Klik Buat Signature lagi, lalu cukup ganti byte jadi ?? tanpa menambah atau menghapus byte.";
            return;
        }

        int count = 0;
        ulong match = 0;
        bool completed = false;

        await RunAsync("Memeriksa pola sebelum disimpan...", async (progress, ct) =>
        {
            var hits = await Task.Run(() => AobScanner.Scan(memory, module, pattern, 2, progress, ct), ct);
            count = hits.Count;
            if (count == 1) match = hits[0];
            completed = true;
        });

        if (!completed) return;

        if (count != 1)
        {
            Message = count == 0
                ? "Pola tidak ditemukan di game ini, jadi tidak disimpan."
                : "Pola cocok di lebih dari satu tempat, jadi tidak disimpan. Perpanjang pola atau kurangi ??.";
            return;
        }

        var signature = new SignatureRef
        {
            Pattern = pattern.ToString(),
            DispOffset = _dispOffset,
            InstructionEnd = _instructionEnd,
            StaticDelta = _staticDelta,
            Offsets = string.Join(',', chain.Offsets.Select(o => o.ToString("X"))),
            Note = _dispOffset >= 0
                ? "Alamat dihitung dari instruksi RIP-relative, lalu mengikuti offset jalur."
                : "Hanya menemukan lokasi kode.",
            CreatedAt = DateTime.Now
        };

        string verified = "";
        if (_dispOffset >= 0)
        {
            var viaSignature = SignatureResolver.ResolveAt(memory, signature, match);
            var viaChain = PointerVerifier.Resolve(memory, chain);

            if (viaSignature is null)
            {
                Message = "Pola unik, tapi alamat yang dihitung darinya belum terbaca (mungkin nilainya belum dimuat di game). " +
                          "Masuk ke bagian game yang memuat nilai ini, lalu coba lagi. Tidak disimpan.";
                return;
            }

            if (viaChain is { } fromChain && fromChain != viaSignature.Value)
            {
                Message = $"Signature menunjuk ke 0x{viaSignature.Value:X}, tetapi jalur utama menunjuk ke 0x{fromChain:X}. " +
                          "Tidak disimpan. Pilih baris kandidat lain.";
                return;
            }

            verified = viaChain is null
                ? " (jalur utama sedang tidak terbaca, jadi belum bisa dicocokkan)"
                : " dan cocok dengan jalur utama";
        }

        Saved.AttachSignature(item, signature);
        UpdateStates();

        Message = $"Signature disimpan ke \"{item.Label}\"{verified}. Klik Tes Signature untuk mengujinya.";
        Session.StatusMessage = $"Signature untuk \"{item.Label}\" tersimpan. Langkah berikutnya: menu Tersimpan atau Tes Signature.";
    }

    [RelayCommand(CanExecute = nameof(CanTestSaved))]
    private async Task TestSavedAsync()
    {
        var memory = Session.Memory;
        var item = SelectedSaved;
        var module = memory?.MainModule;
        if (memory is null || item is null || module is null) return;

        string report = "";
        await RunAsync("Menguji signature tersimpan...", async (progress, ct) =>
        {
            report = await Task.Run(() =>
            {
                var lines = new List<string>();
                ulong? viaChain = item.Chains.Count > 0 ? PointerVerifier.Resolve(memory, item.Chains[0]) : null;
                int size = ValueCodec.SizeOf(item.Saved.ValueType);

                for (int i = 0; i < item.Saved.Signatures.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var sig = item.Saved.Signatures[i];
                    string prefix = $"Signature #{i + 1}: ";

                    if (!AobPattern.TryParse(sig.Pattern, out var pattern, out _))
                    {
                        lines.Add(prefix + "pola rusak.");
                        continue;
                    }

                    var hits = AobScanner.Scan(memory, module, pattern, 2, null, ct);
                    if (hits.Count == 0) { lines.Add(prefix + "tidak ditemukan di game ini."); continue; }
                    if (hits.Count > 1) { lines.Add(prefix + "tidak unik lagi."); continue; }

                    if (sig.DispOffset < 0)
                    {
                        lines.Add(prefix + $"kode ditemukan di {Where(memory, hits[0])}.");
                        continue;
                    }

                    var address = SignatureResolver.ResolveAt(memory, sig, hits[0]);
                    if (address is null)
                    {
                        lines.Add(prefix + $"kode ditemukan di {Where(memory, hits[0])}, tapi alamat nilainya belum terbaca.");
                        continue;
                    }

                    var data = memory.Read(address.Value, size);
                    string value = data is null
                        ? "?"
                        : ValueCodec.Format(item.Saved.ValueType, ValueCodec.ReadRaw(data, 0, size));
                    string compare = viaChain is null ? ""
                        : viaChain.Value == address.Value ? ", sama dengan jalur utama"
                        : $", BEDA dari jalur utama (0x{viaChain.Value:X})";

                    lines.Add(prefix + $"alamat 0x{address.Value:X}, nilai {value}{compare}.");
                }

                return string.Join("  •  ", lines);
            }, ct);
        });

        if (!string.IsNullOrEmpty(report)) Message = report;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    // ---------- logika ----------

    private async Task RunAsync(string busyText, Func<IProgress<double>, CancellationToken, Task> work)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Message = "";
        ScanProgress = 0;
        Summary = busyText;
        IsBusy = true;

        var progress = new Progress<double>(p => ScanProgress = p);

        try
        {
            await work(progress, token);
        }
        catch (OperationCanceledException)
        {
            Message = "Dibatalkan.";
        }
        catch (Exception ex)
        {
            Message = "Gagal: " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
            Summary = "";
        }
    }

    private void ApplyResult(SignatureResult result, ulong delta)
    {
        _dispOffset = result.DispOffset;
        _instructionEnd = result.InstructionEnd;
        _staticDelta = delta;
        _madeLength = result.Pattern!.Length;

        PatternText = result.Pattern.ToString();
        Message = result.Message;
        Hint = AfterMakeHint;
    }

    /// <summary>Akar jalur utama harus di modul utama game (itu yang dicari signature).</summary>
    private bool TryGetRoot(ProcessMemory memory, SavedItem item, out ModuleInfo module, out PointerChain chain)
    {
        module = default!;
        chain = default!;

        if (item.Chains.Count == 0)
        {
            Message = "Alamat tersimpan ini tidak punya jalur yang bisa dipakai.";
            return false;
        }

        var main = memory.MainModule;
        chain = item.Chains[0];
        if (main is null || !string.Equals(chain.ModuleName, main.Name, StringComparison.OrdinalIgnoreCase))
        {
            Message = "Hanya jalur yang berakar di modul utama (exe game) yang didukung.";
            return false;
        }

        module = main;
        return true;
    }

    private static string Where(ProcessMemory memory, ulong address)
    {
        var main = memory.MainModule;
        return main is not null && address >= main.BaseAddress && address < main.BaseAddress + main.Size
            ? $"{main.Name}+{address - main.BaseAddress:X}"
            : $"0x{address:X}";
    }

    private static bool TryParseAddress(string text, ProcessMemory memory, out ulong address)
    {
        address = 0;
        text = text.Trim();

        int plus = text.LastIndexOf('+');
        if (plus > 0)
        {
            string name = text[..plus].Trim();
            var module = memory.Modules.FirstOrDefault(
                m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
            if (module is null || !TryHex(text[(plus + 1)..], out var relative)) return false;

            address = module.BaseAddress + relative;
            return true;
        }

        return TryHex(text, out address);
    }

    private static bool TryHex(string text, out ulong value)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private void UpdateStates()
    {
        FindRefsCommand.NotifyCanExecuteChanged();
        MakeCommand.NotifyCanExecuteChanged();
        MakeFromAddressCommand.NotifyCanExecuteChanged();
        TestCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        TestSavedCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}