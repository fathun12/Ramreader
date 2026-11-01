using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ramreader.App.Services;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Core.Scanning;

namespace Ramreader.App.ViewModels;

public sealed record TypeOption(ScanValueType Type, string Label);
public sealed record ModeOption(ScanMode Mode, string Label);

public partial class ScanViewModel : ObservableObject
{
    private const int MaxRows = 300;

    private const string StartHint =
        "Langkah 1: ketik nilai yang kamu lihat di game (misalnya HP 100), lalu klik Scan Pertama.";

    private readonly ValueScanner _scanner = new();
    private readonly DispatcherTimer _refreshTimer;
    private CancellationTokenSource? _cts;

    public GameSession Session { get; }

    public IReadOnlyList<TypeOption> TypeOptions { get; } = new[]
    {
        new TypeOption(ScanValueType.Int32, "Bilangan bulat 4 byte (int)"),
        new TypeOption(ScanValueType.Float, "Desimal 4 byte (float)"),
        new TypeOption(ScanValueType.Int64, "Bilangan bulat 8 byte (int64)"),
        new TypeOption(ScanValueType.Double, "Desimal 8 byte (double)")
    };

    public IReadOnlyList<ModeOption> ModeOptions { get; } = new[]
    {
        new ModeOption(ScanMode.Exact, "Nilai tepat sama (pakai kolom Nilai)"),
        new ModeOption(ScanMode.Changed, "Nilai berubah"),
        new ModeOption(ScanMode.Unchanged, "Nilai tidak berubah"),
        new ModeOption(ScanMode.Increased, "Nilai bertambah"),
        new ModeOption(ScanMode.Decreased, "Nilai berkurang")
    };

    public ObservableCollection<ScanRow> Rows { get; } = new();

    [ObservableProperty]
    private TypeOption _selectedType;

    [ObservableProperty]
    private ModeOption _selectedMode;

    [ObservableProperty]
    private string _valueInput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty]
    private string _hint = StartHint;

    [ObservableProperty]
    private string _resultSummary = "";

    [ObservableProperty]
    private double _scanProgress;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ScanRow? _selectedRow;

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool NotConnected => !Session.IsConnected;
    public bool CanChangeType => !IsBusy && !_scanner.HasScan;

    public ScanViewModel(GameSession session)
    {
        Session = session;
        _selectedType = TypeOptions[0];
        _selectedMode = ModeOptions[0];

        Session.PropertyChanged += OnSessionChanged;

        // Perbarui nilai baris yang tampil tiap detik, supaya terlihat langsung saat nilai di game berubah.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) => RefreshVisibleValues();
        _refreshTimer.Start();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanChangeType));
        UpdateStates();
    }

    partial void OnSelectedRowChanged(ScanRow? value) => UpdateStates();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(GameSession.Memory) or nameof(GameSession.IsConnected)))
            return;

        OnPropertyChanged(nameof(NotConnected));

        // Alamat hasil scan hanya berlaku untuk satu proses. Ganti/putus game = mulai ulang.
        if (e.PropertyName == nameof(GameSession.Memory))
        {
            if (IsBusy) _cts?.Cancel();
            else ResetScan();
        }

        UpdateStates();
    }

    // ---------- perintah ----------

    private bool CanFirstScan() => Session.IsConnected && !IsBusy;
    private bool CanNextScan() => Session.IsConnected && !IsBusy && _scanner.HasScan;
    private bool CanReset() => !IsBusy;
    private bool CanCancel() => IsBusy;
    private bool CanUseSelected() => SelectedRow is not null && Session.IsConnected;

    [RelayCommand(CanExecute = nameof(CanFirstScan))]
    private async Task FirstScanAsync()
    {
        var memory = Session.Memory;
        if (memory is null) return;

        var type = SelectedType.Type;
        if (!ValueCodec.TryParse(type, ValueInput, out var target))
        {
            Message = "Nilai tidak valid untuk tipe yang dipilih. Contoh: 100 (bulat) atau 100.5 (desimal).";
            return;
        }

        await RunScanAsync(memory, "Memindai memori game...",
            (progress, ct) => _scanner.FirstScan(memory, type, target, progress, ct));
    }

    [RelayCommand(CanExecute = nameof(CanNextScan))]
    private async Task NextScanAsync()
    {
        var memory = Session.Memory;
        if (memory is null) return;

        var mode = SelectedMode.Mode;
        ulong target = 0;
        if (mode == ScanMode.Exact && !ValueCodec.TryParse(_scanner.ValueType, ValueInput, out target))
        {
            Message = "Nilai tidak valid. Ketik nilai barunya di kolom Nilai, atau pilih mode lain (berubah / bertambah / berkurang).";
            return;
        }

        await RunScanAsync(memory, "Menyaring kandidat...",
            (progress, ct) => _scanner.NextScan(memory, mode, target, progress, ct));
    }

    [RelayCommand(CanExecute = nameof(CanReset))]
    private void Reset() => ResetScan();

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanUseSelected))]
    private void SetTarget()
    {
        if (SelectedRow is null) return;

        Session.TargetType = _scanner.ValueType;
        Session.TargetAddress = SelectedRow.Address;
        Session.StatusMessage =
            $"Target {SelectedRow.AddressText} dipilih. Langkah berikutnya: menu Jalur Stabil.";
        Message = $"Target {SelectedRow.AddressText} dipilih untuk pencarian jalur stabil.";
    }

    [RelayCommand(CanExecute = nameof(CanUseSelected))]
    private void CopyAddress()
    {
        if (SelectedRow is null) return;

        try
        {
            Clipboard.SetText(SelectedRow.AddressText);
            Message = $"Alamat {SelectedRow.AddressText} disalin.";
        }
        catch (Exception ex)
        {
            Message = "Gagal menyalin: " + ex.Message;
        }
    }

    // ---------- logika ----------

    private async Task RunScanAsync(ProcessMemory memory, string busyText,
        Action<IProgress<double>, CancellationToken> work)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Message = "";
        ScanProgress = 0;
        ResultSummary = busyText;
        IsBusy = true;

        var progress = new Progress<double>(p => ScanProgress = p);

        try
        {
            await Task.Run(() => work(progress, token));
        }
        catch (OperationCanceledException)
        {
            Message = "Scan dibatalkan.";
        }
        catch (Exception ex)
        {
            Message = "Scan gagal: " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
        }

        // Kalau game diganti/ditutup selama scan, hasilnya sudah tidak berlaku.
        if (!ReferenceEquals(Session.Memory, memory))
            ResetScan();
        else
            ShowResults();
    }

    private void ShowResults()
    {
        Rows.Clear();

        if (_scanner.HasScan)
        {
            var type = _scanner.ValueType;
            foreach (var hit in _scanner.Hits.Take(MaxRows))
            {
                Rows.Add(new ScanRow(hit.Address)
                {
                    ValueText = ValueCodec.Format(type, hit.Value),
                    PreviousText = ValueCodec.Format(type, hit.Previous)
                });
            }
        }

        int count = _scanner.Count;

        ResultSummary = !_scanner.HasScan ? ""
            : count > MaxRows ? $"{count:N0} alamat ditemukan (menampilkan {MaxRows} pertama)"
            : $"{count:N0} alamat ditemukan";

        Hint = !_scanner.HasScan ? StartHint
            : count == 0 ? "Tidak ada yang cocok. Klik Reset dan mulai lagi dengan nilai yang tepat."
            : count <= 10 ? "Sudah sedikit. Pilih alamat yang nilainya ikut berubah di game, lalu klik Jadikan Target."
            : "Langkah 2: ubah nilainya di game (misalnya kena damage), ketik nilai barunya, lalu klik Scan Lanjutan. Ulangi sampai alamatnya tinggal sedikit.";

        if (_scanner.Truncated)
            Message = $"Kandidat melewati batas {ValueScanner.MaxHits:N0} dan hasilnya dipotong, jadi alamat targetmu mungkin tidak ikut. Klik Reset lalu coba nilai yang lebih spesifik.";

        OnPropertyChanged(nameof(CanChangeType));
        UpdateStates();
    }

    private void ResetScan()
    {
        _scanner.Reset();
        Rows.Clear();
        SelectedRow = null;
        ScanProgress = 0;
        ResultSummary = "";
        Hint = StartHint;

        OnPropertyChanged(nameof(CanChangeType));
        UpdateStates();
    }

    private void RefreshVisibleValues()
    {
        var memory = Session.Memory;
        if (memory is null || IsBusy || !_scanner.HasScan || Rows.Count == 0) return;

        var type = _scanner.ValueType;
        foreach (var row in Rows)
        {
            var raw = _scanner.ReadCurrent(memory, row.Address);
            row.ValueText = raw is null ? "?" : ValueCodec.Format(type, raw.Value);
        }
    }

    private void UpdateStates()
    {
        FirstScanCommand.NotifyCanExecuteChanged();
        NextScanCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        SetTargetCommand.NotifyCanExecuteChanged();
        CopyAddressCommand.NotifyCanExecuteChanged();
    }
}