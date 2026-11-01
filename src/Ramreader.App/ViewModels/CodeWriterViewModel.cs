using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ramreader.App.Services;
using Ramreader.Core.Debugging;
using Ramreader.Core.Models;
using Ramreader.Core.Pointers;

namespace Ramreader.App.ViewModels;

/// <summary>Satu baris kandidat penulis. Diperbarui di tempat supaya pilihan tidak hilang.</summary>
public sealed partial class WriterRow : ObservableObject
{
    public WriterRow(WriterCandidate c)
    {
        Address = c.Address;
        InstructionText = c.Instruction;
        LocationText = c.InMainModule ? c.Location : c.Location + " (bukan exe utama)";
        BytesText = c.BytesHex;
        InMainModule = c.InMainModule;
        Update(c);
    }

    public ulong Address { get; }
    public string InstructionText { get; }
    public string LocationText { get; }
    public string BytesText { get; }
    public bool InMainModule { get; }

    [ObservableProperty] private int _hits;
    [ObservableProperty] private int _changes;
    [ObservableProperty] private int _decreases;
    [ObservableProperty] private string _matchText = "";

    public void Update(WriterCandidate c)
    {
        Hits = c.Hits;
        Changes = c.Changes;
        Decreases = c.Decreases;
        MatchText = c.Verified ? "cocok" : "belum pasti";
    }
}

public partial class CodeWriterViewModel : ObservableObject
{
    private const string StartHint =
        "Langkah 1: pilih alamat tersimpan (mis. HP), klik Mulai Pantau, lalu di game biarkan karakter kena serangan beberapa kali. " +
        "Langkah 2: klik Berhenti.";

    private const string TraceHint =
        "Memantau... Sekarang kena serangan di game beberapa kali (dan sembuh sekali kalau bisa). Klik Berhenti kalau sudah.";

    private const string AfterTraceHint =
        "Langkah 3: pilih baris dengan kolom Turun terbesar (itu yang mengurangi nilai), lalu klik Buat Patch. " +
        "Baris yang Hit-nya sangat banyak tapi Berubah 0 biasanya penulis rutin tiap frame, bukan damage.";

    private readonly SavedStore _store;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<ulong, WriterRow> _rowMap = new();
    private WriteTracer? _tracer;
    private CancellationTokenSource? _cts;

    public GameSession Session { get; }
    public SavedViewModel Saved { get; }
    public ObservableCollection<WriterRow> Candidates { get; } = new();

    [ObservableProperty] private SavedItem? _selectedSaved;
    [ObservableProperty] private WriterRow? _selectedCandidate;
    [ObservableProperty] private string _patchLabel = "God Mode";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty] private string _hint = StartHint;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isTracing;
    [ObservableProperty] private bool _isBusy;

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool NotConnected => !Session.IsConnected;
    public bool ShowNoSaved => Session.IsConnected && Saved.IsEmpty;

    public CodeWriterViewModel(GameSession session, SavedViewModel saved, SavedStore store)
    {
        Session = session;
        Saved = saved;
        _store = store;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => SyncRows();

        Session.PropertyChanged += OnSessionChanged;
        Saved.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SavedViewModel.IsEmpty))
                OnPropertyChanged(nameof(ShowNoSaved));
        };
    }

    partial void OnIsTracingChanged(bool value) => UpdateStates();
    partial void OnIsBusyChanged(bool value) => UpdateStates();
    partial void OnSelectedSavedChanged(SavedItem? value) => UpdateStates();
    partial void OnSelectedCandidateChanged(WriterRow? value) => UpdateStates();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(GameSession.Memory) or nameof(GameSession.IsConnected))) return;

        OnPropertyChanged(nameof(NotConnected));
        OnPropertyChanged(nameof(ShowNoSaved));

        if (e.PropertyName == nameof(GameSession.Memory))
        {
            _cts?.Cancel();
            Candidates.Clear();
            _rowMap.Clear();
            SelectedCandidate = null;
            Summary = "";
            Hint = StartHint;
        }

        UpdateStates();
    }

    private bool CanStart() => Session.IsConnected && !IsTracing && !IsBusy && SelectedSaved is not null;
    private bool CanStop() => IsTracing;
    private bool CanMake() =>
        Session.IsConnected && !IsTracing && !IsBusy && SelectedSaved is not null && SelectedCandidate?.InMainModule == true;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var memory = Session.Memory;
        var item = SelectedSaved;
        if (memory is null || item is null) return;

        var resolved = AddressResolver.Resolve(memory, item.Chains, item.Saved.Signatures, item.Saved.ValueType);
        if (resolved.Status != ResolveStatus.Ok)
        {
            Message = "Alamat nilai ini belum ditemukan di game. Masuk ke bagian game yang memuat nilainya (mis. mulai match), lalu coba lagi.";
            return;
        }

        Candidates.Clear();
        _rowMap.Clear();
        SelectedCandidate = null;
        Message = "";

        var tracer = new WriteTracer();
        _tracer = tracer;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var type = item.Saved.ValueType;
        ulong address = resolved.Address;

        IsTracing = true;
        Hint = TraceHint;
        Summary = $"Memantau penulis ke 0x{address:X}...";
        _timer.Start();

        string? error;
        try
        {
            error = await Task.Factory.StartNew(
                () => tracer.Run(memory, address, type, token),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            error = "Gagal: " + ex.Message;
        }

        _timer.Stop();
        SyncRows();
        _cts?.Dispose();
        _cts = null;
        IsTracing = false;

        var sorted = Candidates
            .OrderByDescending(r => r.Decreases)
            .ThenByDescending(r => r.Changes)
            .ThenBy(r => r.Hits)
            .ToList();
        Candidates.Clear();
        foreach (var row in sorted) Candidates.Add(row);
        SelectedCandidate = Candidates.FirstOrDefault();

        Summary = $"{Candidates.Count} instruksi penulis  •  {tracer.TotalHits:N0} hit" +
                  (tracer.Unrecognized > 0 ? $"  •  {tracer.Unrecognized:N0} hit tak dikenali" : "");
        Hint = Candidates.Count > 0 ? AfterTraceHint : StartHint;
        Message = error ?? (Candidates.Count > 0
            ? "Pemantauan berhenti dan game sudah dilepas dari debugger."
            : "Pemantauan berhenti, tapi tidak ada penulis yang tertangkap. Pastikan nilainya benar-benar berubah di game saat memantau.");
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanMake))]
    private async Task MakePatchAsync()
    {
        var memory = Session.Memory;
        var item = SelectedSaved;
        var row = SelectedCandidate;
        var module = memory?.MainModule;
        if (memory is null || item is null || row is null || module is null) return;

        string label = string.IsNullOrWhiteSpace(PatchLabel) ? "God Mode" : PatchLabel.Trim();
        ulong address = row.Address;

        CodePatchResult? result = null;
        IsBusy = true;
        Message = "";
        Summary = "Membuat signature unik untuk patch (bisa beberapa detik)...";
        try
        {
            result = await Task.Run(() =>
                CodePatchMaker.Make(memory, module, address, label, null, CancellationToken.None));
        }
        catch (Exception ex)
        {
            Message = "Gagal: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            Summary = "";
        }

        if (result is null) return;
        if (result.Patch is null)
        {
            Message = result.Message;
            return;
        }

        try
        {
            item.Saved.CodePatches.RemoveAll(p => string.Equals(p.Label, label, StringComparison.OrdinalIgnoreCase));
            item.Saved.CodePatches.Add(result.Patch);
            _store.Save(memory.ProcessName, Saved.Items.Select(i => i.Saved).ToList());
        }
        catch (Exception ex)
        {
            Message = "Patch dibuat tapi gagal disimpan: " + ex.Message;
            return;
        }

        Message = result.Message + $" Tersimpan ke \"{item.Label}\" sebagai \"{label}\". Buka Trainer: kartu God Mode muncul di sana.";
        Session.StatusMessage = $"Patch \"{label}\" tersimpan. Langkah berikutnya: buka Trainer dan aktifkan God Mode.";
    }

    private void SyncRows()
    {
        var tracer = _tracer;
        if (tracer is null) return;

        foreach (var c in tracer.Snapshot())
        {
            if (_rowMap.TryGetValue(c.Address, out var row))
            {
                row.Update(c);
            }
            else
            {
                row = new WriterRow(c);
                _rowMap[c.Address] = row;
                Candidates.Add(row);
            }
        }

        if (IsTracing)
            Summary = $"Memantau...  {Candidates.Count} instruksi  •  {tracer.TotalHits:N0} hit";
    }

    private void UpdateStates()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        MakePatchCommand.NotifyCanExecuteChanged();
    }
}