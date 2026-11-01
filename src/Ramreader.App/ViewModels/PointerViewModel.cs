using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ramreader.App.Services;
using Ramreader.Core.Models;
using Ramreader.Core.Pointers;

namespace Ramreader.App.ViewModels;

public sealed record DepthOption(int Depth, string Label);
public sealed record OffsetOption(ulong MaxOffset, string Label);

public partial class PointerViewModel : ObservableObject
{
    private const int MaxRows = 200;

    private const string NeedTargetHint =
        "Langkah 1: di menu Cari Nilai, temukan alamat nilainya (HP, uang, dst) sampai tinggal sedikit, pilih satu, lalu klik Jadikan Target.";

    private const string AfterFindHint =
        "Langkah 2: TUTUP game lalu buka lagi (Ramreader jangan ditutup). Hubungkan lagi di menu Proses, " +
        "cari nilai yang sama di Cari Nilai, klik Jadikan Target, lalu kembali ke sini dan klik Verifikasi.";

    private readonly PointerScanner _scanner = new();
    private readonly ChainStore _store;
    private readonly SavedViewModel _saved;

    private List<PointerChain> _chains = new();
    private string? _chainsProcess;
    private int _lastPid;
    private CancellationTokenSource? _cts;

    public GameSession Session { get; }

    public IReadOnlyList<DepthOption> DepthOptions { get; } = new[]
    {
        new DepthOption(2, "Cepat (2 level)"),
        new DepthOption(3, "Normal (3 level)"),
        new DepthOption(4, "Dalam (4 level)"),
        new DepthOption(5, "Sangat dalam (5 level)")
    };

    public IReadOnlyList<OffsetOption> OffsetOptions { get; } = new[]
    {
        new OffsetOption(0x400, "Kecil (0x400)"),
        new OffsetOption(0x800, "Sedang (0x800)"),
        new OffsetOption(0x1000, "Besar (0x1000)"),
        new OffsetOption(0x2000, "Sangat besar (0x2000)")
    };

    public ObservableCollection<ChainRow> Rows { get; } = new();

    [ObservableProperty]
    private DepthOption _selectedDepth;

    [ObservableProperty]
    private OffsetOption _selectedOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty]
    private string _hint = NeedTargetHint;

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private double _scanProgress;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _rounds;

    [ObservableProperty]
    private ChainRow? _selectedRow;

    /// <summary>True saat kotak label "Simpan Alamat" sedang terbuka.</summary>
    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _saveLabel = "";

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool NotConnected => !Session.IsConnected;
    public bool ShowNoTarget => Session.IsConnected && Session.TargetAddress is null;
    public string TargetText => Session.TargetAddress is { } t ? $"Target: 0x{t:X}" : "Target: belum dipilih";

    public PointerViewModel(GameSession session, ChainStore store, SavedViewModel saved)
    {
        Session = session;
        _store = store;
        _saved = saved;
        _selectedDepth = DepthOptions[2];
        _selectedOffset = OffsetOptions[1];

        Session.PropertyChanged += OnSessionChanged;
    }

    partial void OnIsBusyChanged(bool value) => UpdateStates();
    partial void OnSelectedRowChanged(ChainRow? value) => UpdateStates();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(GameSession.Memory)
            or nameof(GameSession.IsConnected)
            or nameof(GameSession.TargetAddress)))
            return;

        if (e.PropertyName == nameof(GameSession.Memory))
        {
            if (IsBusy) _cts?.Cancel();

            var memory = Session.Memory;
            if (memory is not null &&
                !string.Equals(_chainsProcess, memory.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                LoadStored(memory.ProcessName);
            }

            ShowRows();
        }

        OnPropertyChanged(nameof(NotConnected));
        OnPropertyChanged(nameof(ShowNoTarget));
        OnPropertyChanged(nameof(TargetText));
        RefreshHint();
        UpdateStates();
    }

    // ---------- perintah ----------

    private bool CanFind() => Session.IsConnected && Session.TargetAddress is not null && !IsBusy;
    private bool CanVerify() => CanFind() && _chains.Count > 0;
    private bool CanClear() => !IsBusy && _chains.Count > 0;
    private bool CanCancel() => IsBusy;
    private bool CanCopy() => SelectedRow is not null;

    [RelayCommand(CanExecute = nameof(CanFind))]
    private async Task FindAsync()
    {
        var memory = Session.Memory;
        var target = Session.TargetAddress;
        if (memory is null || target is null) return;

        int depth = SelectedDepth.Depth;
        ulong maxOffset = SelectedOffset.MaxOffset;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Message = "";
        ScanProgress = 0;
        ProgressText = "Memulai pencarian...";
        IsBusy = true;

        var progress = new Progress<PointerScanProgress>(p =>
        {
            ScanProgress = p.Fraction;
            ProgressText = p.Message;
        });

        PointerScanResult? result = null;
        try
        {
            result = await Task.Run(() => _scanner.Scan(memory, target.Value, depth, maxOffset, progress, token));
        }
        catch (OperationCanceledException)
        {
            Message = "Pencarian dibatalkan.";
        }
        catch (Exception ex)
        {
            Message = "Pencarian gagal: " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
        }

        ProgressText = "";
        if (result is null) return;

        if (result.Chains.Count == 0)
        {
            Message = "Tidak ada jalur ke modul utama game. Coba kedalaman atau jarak offset yang lebih besar. " +
                      "Kalau tetap kosong, data ini mungkin dijangkau lewat DLL lain atau struktur yang tidak bisa dilacak dengan cara ini.";
            return;
        }

        _chains = result.Chains;
        _chainsProcess = memory.ProcessName;
        _lastPid = memory.ProcessId;
        Rounds = 0;

        SaveStore(memory.ProcessName);
        ShowRows();

        if (result.Truncated)
            Message = "Jumlah jalur melewati batas dan sebagian dipotong. Jalur terpendek tetap disimpan.";

        Session.StatusMessage = "Jalur kandidat tersimpan. Langkah berikutnya: tutup dan buka lagi game, lalu Verifikasi.";
    }

    [RelayCommand(CanExecute = nameof(CanVerify))]
    private async Task VerifyAsync()
    {
        var memory = Session.Memory;
        var target = Session.TargetAddress;
        if (memory is null || target is null) return;

        if (memory.ProcessId == _lastPid)
        {
            Message = "Game belum direstart sejak pencarian/verifikasi terakhir, jadi verifikasi belum berguna. " +
                      "Tutup lalu buka lagi game-nya, hubungkan ulang, cari nilainya, Jadikan Target, baru Verifikasi.";
            return;
        }

        var chains = _chains;
        int before = chains.Count;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Message = "";
        ScanProgress = 0;
        ProgressText = "Memverifikasi jalur...";
        IsBusy = true;

        List<PointerChain>? survivors = null;
        try
        {
            survivors = await Task.Run(() => PointerVerifier.Filter(memory, chains, target.Value, token));
        }
        catch (OperationCanceledException)
        {
            Message = "Verifikasi dibatalkan.";
        }
        catch (Exception ex)
        {
            Message = "Verifikasi gagal: " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
        }

        ProgressText = "";
        if (survivors is null) return;

        if (survivors.Count == 0)
        {
            ShowRows();

            int readable = 0;
            ulong? firstResolved = null;
            foreach (var chain in chains.Take(MaxRows))
            {
                var resolved = PointerVerifier.Resolve(memory, chain);
                if (resolved is null) continue;
                readable++;
                firstResolved ??= resolved;
            }

            Message = "Tidak ada jalur yang cocok dengan target ini. Kandidat lama TIDAK dihapus. " +
                      $"Dari {Math.Min(chains.Count, MaxRows)} jalur teratas, {readable} bisa dibaca di game sekarang" +
                      (firstResolved is { } first ? $" (yang pertama menghasilkan 0x{first:X}, target 0x{target.Value:X}). " : ". ") +
                      "Kalau yang bisa dibaca nol, jalurnya putus di sesi ini. Kalau ada tapi alamatnya beda, jalurnya benar tapi menuju objek lain.";
            return;
        }

        _chains = survivors;
        _lastPid = memory.ProcessId;
        Rounds++;

        SaveStore(memory.ProcessName);
        ShowRows();

        Message = $"{before:N0} jalur diperiksa, {survivors.Count:N0} bertahan.";
        Session.StatusMessage = $"Verifikasi ronde {Rounds}: {survivors.Count:N0} jalur bertahan.";
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        if (_chainsProcess is not null) _store.Delete(_chainsProcess);

        _chains = new List<PointerChain>();
        Rounds = 0;
        _lastPid = 0;
        Message = "Kandidat dihapus.";
        ShowRows();
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void CopyChain()
    {
        if (SelectedRow is null) return;

        try
        {
            Clipboard.SetText(SelectedRow.Description);
            Message = "Jalur disalin.";
        }
        catch (Exception ex)
        {
            Message = "Gagal menyalin: " + ex.Message;
        }
    }

    private const int MaxSavedChains = 20;

    private bool CanOpenSave() => Session.IsConnected && !IsBusy && _chains.Count > 0;

    [RelayCommand(CanExecute = nameof(CanOpenSave))]
    private void OpenSave()
    {
        Message = Rounds == 0
            ? "Jalur ini belum diverifikasi. Boleh disimpan, tapi lebih aman restart game dan Verifikasi dulu."
            : "";
        IsSaving = true;
    }

    [RelayCommand]
    private void CancelSave()
    {
        IsSaving = false;
        SaveLabel = "";
    }

    [RelayCommand]
    private void ConfirmSave()
    {
        var memory = Session.Memory;
        if (memory is null || _chains.Count == 0)
        {
            Message = "Tidak ada jalur untuk disimpan. Hubungkan ke game dan cari jalur dulu.";
            IsSaving = false;
            return;
        }

        var label = SaveLabel.Trim();
        if (label.Length == 0)
        {
            Message = "Isi label dulu, misalnya \"Tiket\" atau \"Uang\".";
            return;
        }

        if (!string.Equals(_chainsProcess, memory.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            Message = "Jalur ini berasal dari game lain. Cari jalur lagi untuk game yang sedang terhubung.";
            return;
        }

        var saved = new SavedAddress
        {
            Label = label,
            ValueType = Session.TargetType ?? ScanValueType.Int32,
            VerifiedRounds = Rounds,
            MainModuleSize = memory.MainModule?.Size ?? 0,
            SavedAt = DateTime.Now,
            Chains = _chains.Take(MaxSavedChains).Select(c => c.Serialize()).ToList()
        };

        _saved.Add(saved);

        IsSaving = false;
        SaveLabel = "";
        Message = $"\"{label}\" disimpan ({saved.Chains.Count} jalur). Lihat di menu Tersimpan.";
        Session.StatusMessage = $"\"{label}\" tersimpan. Langkah berikutnya: menu Tersimpan untuk ekspor.";
    }

    // ---------- logika ----------

    private void LoadStored(string processName)
    {
        var stored = _store.Load(processName);
        _chains = stored.Chains;
        Rounds = stored.Rounds;
        _chainsProcess = processName;
        _lastPid = 0;
        SelectedRow = null;
    }

    private void SaveStore(string processName)
    {
        try
        {
            _store.Save(processName, _chains, Rounds);
        }
        catch (Exception ex)
        {
            Message = "Gagal menyimpan ke disk (jalur tetap ada selama aplikasi terbuka): " + ex.Message;
        }
    }

    private void ShowRows()
    {
        Rows.Clear();
        SelectedRow = null;

        var memory = Session.Memory;
        foreach (var chain in _chains.Take(MaxRows))
        {
            ulong? resolved = memory is null ? null : PointerVerifier.Resolve(memory, chain);
            Rows.Add(new ChainRow(chain, resolved is null ? "-" : $"0x{resolved.Value:X}"));
        }

        Summary = _chains.Count == 0
            ? ""
            : $"{_chains.Count:N0} jalur kandidat  •  ronde verifikasi: {Rounds}"
              + (_chains.Count > MaxRows ? $"  •  menampilkan {MaxRows} terpendek" : "");

        RefreshHint();
        UpdateStates();
    }

    private void RefreshHint()
    {
        if (_chains.Count == 0)
            Hint = Session.TargetAddress is null
                ? NeedTargetHint
                : "Target sudah dipilih. Klik Cari Jalur untuk memulai.";
        else if (Rounds == 0)
            Hint = AfterFindHint;
        else if (_chains.Count <= 5)
            Hint = $"Sangat sedikit ({_chains.Count}). Jalur teratas biasanya sudah layak dipakai. " +
                   "Untuk lebih yakin, ulangi restart game + Verifikasi sekali lagi.";
        else
            Hint = $"Masih {_chains.Count:N0} jalur. Tutup dan buka game lagi, ulangi cari nilai + Jadikan Target, " +
                   "lalu Verifikasi. Tiap ronde biasanya memangkas jumlahnya drastis.";
    }

    private void UpdateStates()
    {
        FindCommand.NotifyCanExecuteChanged();
        VerifyCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        CopyChainCommand.NotifyCanExecuteChanged();
        OpenSaveCommand.NotifyCanExecuteChanged();
    }
}