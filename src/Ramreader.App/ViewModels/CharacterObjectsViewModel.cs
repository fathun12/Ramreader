using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ramreader.App.Services;
using Ramreader.Core.Debugging;
using Ramreader.Core.Models;
using Ramreader.Core.Pointers;

namespace Ramreader.App.ViewModels;

/// <summary>Satu objek karakter yang tertangkap. Diperbarui di tempat supaya pilihan tidak hilang.</summary>
public sealed partial class ObjectRow : ObservableObject
{
    public ObjectRow(ulong baseAddress, bool isMine)
    {
        Base = baseAddress;
        BaseText = $"0x{baseAddress:X}";
        IsMine = isMine;
        if (isMine) Role = "Tim";
    }

    public ulong Base { get; }
    public string BaseText { get; }
    public bool IsMine { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleText))]
    private string _role = "?";

    [ObservableProperty] private int _hits;
    [ObservableProperty] private string _valueText = "-";

    public string RoleText => IsMine ? "Tim (aku)" : Role;
    public bool IsTeam => IsMine || Role == "Tim";
    public bool IsEnemy => !IsMine && Role == "Musuh";
}

public sealed class MarkerRow
{
    public MarkerRow(MarkerCandidate c)
    {
        OffsetText = $"+0x{c.Offset:X}";
        TeamText = $"0x{c.TeamValue:X8} ({c.TeamValue})";
        EnemyText = c.EnemyValues;
    }

    public string OffsetText { get; }
    public string TeamText { get; }
    public string EnemyText { get; }
}

public partial class CharacterObjectsViewModel : ObservableObject
{
    private const int ObjectSize = 0x800;
    private const int MaxShown = 300;

    private const string StartHint =
        "Langkah 1: pilih alamat tersimpan (mis. HP atau Chakra). Kolom Instruksi dikosongkan = otomatis dari patch milik alamat itu. " +
        "Klik Mulai Pantau, biarkan game jalan beberapa detik, lalu Berhenti. Baris \"Tim (aku)\" harus muncul otomatis.";

    private const string MarkHint =
        "Langkah 2: tandai teman satu timmu (pilih baris, klik Tandai Tim). Tips: ganti ke teman lewat tag, pakai jurus, " +
        "klik Berhenti-Mulai lagi; baris yang kolom Nilai-nya berubah adalah dia. Lalu klik Sisanya Musuh. " +
        "Langkah 3: klik Bandingkan. Ubah keadaan game (kena pukul, pakai jurus, ganti karakter) dan Bandingkan lagi; hasilnya diiriskan.";

    private readonly DispatcherTimer _timer;
    private readonly Dictionary<ulong, ObjectRow> _rowMap = new();
    private Dictionary<int, MarkerCandidate>? _kept;
    private int _rounds;
    private ObjectTracer? _tracer;
    private CancellationTokenSource? _cts;
    private ulong? _playerBase;
    private long _disp;
    private ScanValueType _valueType = ScanValueType.Float;

    public GameSession Session { get; }
    public SavedViewModel Saved { get; }
    public ObservableCollection<ObjectRow> Objects { get; } = new();
    public ObservableCollection<MarkerRow> Candidates { get; } = new();

    [ObservableProperty] private SavedItem? _selectedSaved;
    [ObservableProperty] private ObjectRow? _selectedObject;
    [ObservableProperty] private string _instructionText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty] private string _hint = StartHint;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _candidateSummary = "";
    [ObservableProperty] private bool _isTracing;

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool NotConnected => !Session.IsConnected;
    public bool ShowNoSaved => Session.IsConnected && Saved.IsEmpty;

    public CharacterObjectsViewModel(GameSession session, SavedViewModel saved)
    {
        Session = session;
        Saved = saved;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => SyncRows();
        _timer.Start();

        Session.PropertyChanged += OnSessionChanged;
        Saved.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SavedViewModel.IsEmpty))
                OnPropertyChanged(nameof(ShowNoSaved));
        };
    }

    partial void OnIsTracingChanged(bool value) => UpdateStates();
    partial void OnSelectedSavedChanged(SavedItem? value)
    {
        InstructionText = "";
        UpdateStates();
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(GameSession.Memory) or nameof(GameSession.IsConnected))) return;

        OnPropertyChanged(nameof(NotConnected));
        OnPropertyChanged(nameof(ShowNoSaved));

        if (e.PropertyName == nameof(GameSession.Memory))
        {
            _cts?.Cancel();
            Objects.Clear();
            _rowMap.Clear();
            Candidates.Clear();
            _kept = null;
            _rounds = 0;
            _playerBase = null;
            SelectedObject = null;
            Summary = "";
            CandidateSummary = "";
            Hint = StartHint;
        }

        UpdateStates();
    }

    private bool CanStart() => Session.IsConnected && !IsTracing && SelectedSaved is not null;
    private bool CanStop() => IsTracing;

    private static bool TryParseOffset(string text, out ulong offset)
    {
        string t = (text ?? "").Trim();
        int plus = t.LastIndexOf('+');
        if (plus >= 0) t = t[(plus + 1)..].Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return ulong.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var memory = Session.Memory;
        var item = SelectedSaved;
        var module = memory?.MainModule;
        if (memory is null || item is null || module is null) return;

        if (string.IsNullOrWhiteSpace(InstructionText))
        {
            var patch = item.Saved.CodePatches?.FirstOrDefault();
            if (patch is null)
            {
                Message = "Alamat tersimpan ini belum punya code patch. Buat dulu di menu Penulis Kode, atau ketik offset instruksi manual.";
                return;
            }

            Message = "Mencari instruksi dari patch '" + patch.Label + "' lewat signature (beberapa detik)...";
            var (foundAddress, foundMessage) = await Task.Run(() => CodePatchLocator.Find(memory, patch, CancellationToken.None));
            if (foundAddress is not { } found)
            {
                Message = "Instruksi tidak ditemukan: " + foundMessage;
                return;
            }

            InstructionText = (found - module.BaseAddress).ToString("X");
            Message = "";
        }

        if (!TryParseOffset(InstructionText, out ulong offset))
        {
            Message = "Offset instruksi harus angka hex, mis. 94BC94 atau NSUNSC.exe+94BC94.";
            return;
        }

        ulong instruction = module.BaseAddress + offset;
        var target = ObjectTracer.Analyze(memory, instruction, out string analyzeError);
        if (target is null)
        {
            Message = analyzeError;
            return;
        }

        _disp = target.Disp;
        _valueType = item.Saved.ValueType;
        _playerBase = null;

        var resolved = AddressResolver.Resolve(memory, item.Chains, item.Saved.Signatures, item.Saved.ValueType);
        if (resolved.Status == ResolveStatus.Ok)
            _playerBase = unchecked(resolved.Address - (ulong)target.Disp);

        Objects.Clear();
        _rowMap.Clear();
        SelectedObject = null;
        Message = "";

        var tracer = new ObjectTracer();
        _tracer = tracer;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        int baseReg = target.BaseReg;

        IsTracing = true;
        Hint = MarkHint;
        Summary = $"Memantau {target.Instruction} di {module.Name}+{offset:X}...";

        string? error;
        try
        {
            error = await Task.Factory.StartNew(
                () => tracer.Run(memory, instruction, baseReg, token),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            error = "Gagal: " + ex.Message;
        }

        _cts?.Dispose();
        _cts = null;
        IsTracing = false;
        SyncRows();

        Summary = $"{Objects.Count} objek  |  {tracer.TotalHits:N0} hit";

        if (error is not null)
        {
            Message = error;
        }
        else if (Objects.Count == 0)
        {
            Message = "Tidak ada objek tertangkap. Pastikan instruksinya jalan di game saat memantau (masuk ke match).";
        }
        else if (_playerBase is { } pb && !_rowMap.ContainsKey(pb))
        {
            Message = $"Objek milikmu (0x{pb:X}) tidak tertangkap. Kemungkinan alamat tersimpan dan instruksi tidak cocok " +
                      "(mis. alamat HP dengan instruksi chakra), atau karakter tidak sedang aktif. Peran Tim belum otomatis.";
        }
        else
        {
            Message = "Pemantauan berhenti dan game sudah dilepas dari debugger.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    [RelayCommand]
    private void MarkTeam()
    {
        if (SelectedObject is { } row) row.Role = "Tim";
    }

    [RelayCommand]
    private void MarkEnemy()
    {
        if (SelectedObject is { } row && !row.IsMine) row.Role = "Musuh";
    }

    [RelayCommand]
    private void MarkRestEnemy()
    {
        foreach (var row in Objects)
        {
            if (!row.IsMine && row.Role == "?") row.Role = "Musuh";
        }
    }

    [RelayCommand]
    private void AutoMark()
    {
        const int markerOffset = 0x44;

        var memory = Session.Memory;
        if (memory is null) return;

        var mine = Objects.FirstOrDefault(o => o.IsMine);
        if (mine is null)
        {
            Message = "Baris Tim (aku) belum ada. Pilih alamat tersimpan dan instruksi yang cocok, lalu Mulai Pantau dan pakai karakter sampai barisnya muncul.";
            return;
        }

        if (!memory.TryRead<uint>(mine.Base + (ulong)markerOffset, out uint myMarker))
        {
            Message = "Penanda milikmu di +0x44 gagal dibaca.";
            return;
        }

        int team = 1, enemy = 0, unread = 0;
        var enemyValues = new SortedSet<uint>();
        foreach (var row in Objects)
        {
            if (row.IsMine) continue;
            if (!memory.TryRead<uint>(row.Base + (ulong)markerOffset, out uint value)) { unread++; continue; }

            if (value == myMarker) { row.Role = "Tim"; team++; }
            else { row.Role = "Musuh"; enemy++; enemyValues.Add(value); }
        }

        Message = $"Otomatis: {team} Tim, {enemy} Musuh (penanda +0x{markerOffset:X} milikmu 0x{myMarker:X8}). " +
                  "Nilai penanda Musuh: " + (enemyValues.Count == 0 ? "-" : string.Join(", ", enemyValues.Select(v => $"0x{v:X8}"))) +
                  (unread > 0 ? $". {unread} objek gagal dibaca." : ".");
    }

    [RelayCommand]
    private void ClearMarks()
    {
        foreach (var row in Objects)
        {
            if (!row.IsMine) row.Role = "?";
        }
    }

    [RelayCommand]
    private void Compare()
    {
        var memory = Session.Memory;
        if (memory is null) return;

        var samples = new List<ObjectSample>();
        int skipped = 0;
        foreach (var row in Objects)
        {
            if (!row.IsTeam && !row.IsEnemy) continue;

            var data = memory.Read(row.Base, ObjectSize);
            if (data is null) { skipped++; continue; }
            samples.Add(new ObjectSample(row.Base, row.IsTeam, data));
        }

        int teamCount = samples.Count(s => s.IsTeam);
        int enemyCount = samples.Count - teamCount;
        if (teamCount == 0 || enemyCount == 0)
        {
            Message = "Tandai minimal satu Tim dan satu Musuh dulu (objek yang gagal dibaca: " + skipped + ").";
            return;
        }

        var found = ObjectComparer.Compare(samples);

        if (_kept is null)
        {
            _kept = found.ToDictionary(c => c.Offset);
            _rounds = 1;
        }
        else
        {
            var next = new Dictionary<int, MarkerCandidate>();
            foreach (var c in found)
            {
                if (_kept.TryGetValue(c.Offset, out var old) && old.TeamValue == c.TeamValue)
                    next[c.Offset] = c;
            }
            _kept = next;
            _rounds++;
        }

        ShowCandidates(teamCount, enemyCount, skipped);
    }

    [RelayCommand]
    private void ResetRounds()
    {
        _kept = null;
        _rounds = 0;
        Candidates.Clear();
        CandidateSummary = "";
        Message = "Irisan dikosongkan. Tanda Tim/Musuh tetap ada. Klik Bandingkan untuk mulai ronde 1 lagi.";
    }

    private void ShowCandidates(int teamCount, int enemyCount, int skipped)
    {
        Candidates.Clear();
        if (_kept is null) return;

        foreach (var c in _kept.Values.OrderBy(c => c.Offset).Take(MaxShown))
            Candidates.Add(new MarkerRow(c));

        CandidateSummary = $"{_kept.Count} kandidat penanda  |  ronde {_rounds}  |  {teamCount} Tim, {enemyCount} Musuh" +
                           (skipped > 0 ? $"  |  {skipped} objek gagal dibaca" : "") +
                           (_kept.Count > MaxShown ? $"  |  hanya {MaxShown} pertama tampil" : "");

        Message = _kept.Count switch
        {
            0 => "Tidak ada offset yang memisahkan Tim dan Musuh secara konsisten. Cek tanda Tim/Musuh, atau Ulangi Irisan.",
            <= 20 => "Sedikit. Laporkan daftar offset ini ke aku (offset, nilai Tim, nilai Musuh).",
            _ => "Masih banyak. Ubah keadaan game (kena pukul, pakai jurus, ganti karakter), lalu Bandingkan lagi."
        };
    }

    private void SyncRows()
    {
        var tracer = _tracer;
        if (tracer is not null)
        {
            foreach (var o in tracer.Snapshot())
            {
                if (!_rowMap.TryGetValue(o.Base, out var row))
                {
                    row = new ObjectRow(o.Base, o.Base == _playerBase);
                    _rowMap[o.Base] = row;
                    Objects.Add(row);
                }
                row.Hits = o.Hits;
            }

            if (IsTracing)
                Summary = $"Memantau...  {Objects.Count} objek  |  {tracer.TotalHits:N0} hit";
        }

        var memory = Session.Memory;
        if (memory is null) return;

        foreach (var row in Objects)
            row.ValueText = ReadValue(memory, row.Base);
    }

    private string ReadValue(Ramreader.Core.Memory.ProcessMemory memory, ulong baseAddress)
    {
        var bytes = memory.Read(unchecked(baseAddress + (ulong)_disp), 4);
        if (bytes is null) return "?";

        return _valueType switch
        {
            ScanValueType.Float => BitConverter.ToSingle(bytes, 0).ToString("0.##", CultureInfo.InvariantCulture),
            ScanValueType.Int32 => BitConverter.ToInt32(bytes, 0).ToString(CultureInfo.InvariantCulture),
            _ => "0x" + BitConverter.ToUInt32(bytes, 0).ToString("X8")
        };
    }

    private void UpdateStates()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }
}