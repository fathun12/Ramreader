using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Core.Pointers;
using Ramreader.Core.Scanning;
using Ramreader.Trainer.Services;

namespace Ramreader.Trainer.ViewModels;

/// <summary>Satu fitur di trainer = satu alamat dari profil: baca, Set, Freeze, pengaturan, hotkey.</summary>
public partial class FeatureItem : ObservableObject
{
    private const string DefaultMin = "0";
    private const string DefaultMax = "1000000000";

    private sealed record RangeBox(double Min, double Max);

    private readonly TrainerSession _session;
    private ulong _frozenRaw;
    private ulong? _lastRaw;
    private volatile bool _freezeHeld;
    private volatile RangeBox? _range;
    private bool _applyingSettings;

    public FeatureItem(SavedAddress saved, TrainerSession session)
    {
        Saved = saved;
        _session = session;
        foreach (var line in saved.Chains)
        {
            if (PointerChain.TryParse(line, out var chain))
                Chains.Add(chain);
        }
        RefreshRange();
    }

    public SavedAddress Saved { get; }
    public List<PointerChain> Chains { get; } = new();
    public string Label => Saved.Label;
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? Saved.Label : DisplayName.Trim();

    /// <summary>Alamat nilai di game pada pembacaan terakhir (null kalau belum terbaca).</summary>
    public ulong? CurrentAddress { get; private set; }

    /// <summary>Dipanggil kalau salah satu kolom Pengaturan berubah (untuk menyimpan dan mendaftar ulang hotkey).</summary>
    public event Action? SettingsChanged;

    /// <summary>Tombol Pengaturan di kartu: minta panel pengaturan samping dibuka untuk fitur ini.</summary>
    public event Action<FeatureItem>? SettingsRequested;

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string _displayName = "";

    [ObservableProperty]
    private string _minText = DefaultMin;

    [ObservableProperty]
    private string _maxText = DefaultMax;

    [ObservableProperty]
    private string _presetText = "";

    [ObservableProperty]
    private string _hotkeySetText = "";

    [ObservableProperty]
    private string _hotkeyFreezeText = "";

    [ObservableProperty]
    private string _hotkeyStatus = "";

    [ObservableProperty]
    private string _valueText = "-";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _inputText = "";

    [ObservableProperty]
    private string _actionText = "";

    [ObservableProperty]
    private bool _isFrozen;

    // ---------- pengaturan ----------

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(MinText) or nameof(MaxText)) RefreshRange();
        if (_applyingSettings) return;

        if (e.PropertyName is nameof(DisplayName) or nameof(MinText) or nameof(MaxText)
            or nameof(PresetText) or nameof(HotkeySetText) or nameof(HotkeyFreezeText))
            SettingsChanged?.Invoke();
    }

    public FeatureSettings ToSettings() => new()
    {
        DisplayName = DisplayName,
        Min = MinText,
        Max = MaxText,
        Preset = PresetText,
        HotkeySet = HotkeySetText,
        HotkeyFreeze = HotkeyFreezeText,
        AutoFreeze = AutoFreeze
    };

    public void ApplySettings(FeatureSettings s)
    {
        _applyingSettings = true;
        try
        {
            DisplayName = s.DisplayName ?? "";
            MinText = string.IsNullOrWhiteSpace(s.Min) ? DefaultMin : s.Min;
            MaxText = string.IsNullOrWhiteSpace(s.Max) ? DefaultMax : s.Max;
            PresetText = s.Preset ?? "";
            HotkeySetText = s.HotkeySet ?? "";
            HotkeyFreezeText = s.HotkeyFreeze ?? "";
            AutoFreeze = s.AutoFreeze;
        }
        finally
        {
            _applyingSettings = false;
        }
        RefreshRange();
    }

    private void RefreshRange() =>
        _range = TryGetRange(out double min, out double max) ? new RangeBox(min, max) : null;

    private bool TryGetRange(out double min, out double max)
    {
        max = 0;
        if (!TryNumber(MinText, out min) || !TryNumber(MaxText, out max)) return false;
        return min <= max;
    }

    private static bool TryNumber(string? text, out double value) =>
        double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private string RangeText => $"{MinText.Trim()} sampai {MaxText.Trim()}";

    private bool IsSane(ulong raw)
    {
        var range = _range;
        if (range is null) return false;

        double v = ToDouble(Saved.ValueType, raw);
        return !double.IsNaN(v) && !double.IsInfinity(v) && v >= range.Min && v <= range.Max;
    }

    private string NotSane(ulong raw) =>
        _range is null
            ? "Batas rentang aman di Pengaturan tidak valid (isi angka, minimum tidak lebih besar dari maksimum). Tidak ada yang ditulis."
            : $"Nilai {ValueCodec.Format(Saved.ValueType, raw)} ditolak: di luar rentang aman ({RangeText}).";

    // ---------- membaca (timer 1 detik) ----------

    public void Update(ProcessMemory? memory)
    {
        TryAutoFreeze(memory);
        CurrentAddress = null;
        _lastRaw = null;

        if (memory is null)
        {
            ValueText = "-";
            StatusText = "Menunggu game...";
            if (IsFrozen) IsFrozen = false;   // game putus: kunci selalu dilepas
            return;
        }

        var result = AddressResolver.Resolve(memory, Chains, Saved.Signatures, Saved.ValueType);

        switch (result.Status)
        {
            case ResolveStatus.Ok when result.Source == ResolveSource.Chain:
                CurrentAddress = result.Address;
                _lastRaw = result.Raw;
                ValueText = ValueCodec.Format(Saved.ValueType, result.Raw);
                StatusText = result.Index == 0
                    ? "Alamat ditemukan lewat jalur utama."
                    : $"Alamat ditemukan lewat jalur cadangan #{result.Index + 1}.";
                return;

            case ResolveStatus.Ok:
                CurrentAddress = result.Address;
                _lastRaw = result.Raw;
                ValueText = ValueCodec.Format(Saved.ValueType, result.Raw);
                StatusText = $"Jalur putus, alamat ditemukan lewat signature #{result.Index + 1}.";
                return;

            case ResolveStatus.SignatureSearching:
                ValueText = "...";
                StatusText = "Jalur putus. Mencari lewat signature (sekali saja, bisa beberapa detik)...";
                return;
        }

        ValueText = "?";
        StatusText = "Alamat tidak ditemukan. Mungkin game belum sampai layar yang memuat nilai ini, atau game sudah di-update.";
    }

    // ---------- Set ----------

    /// <summary>Tombol Set: pakai kotak nilai; kalau kosong pakai Preset.</summary>
    [RelayCommand]
    private void Apply()
    {
        string text = string.IsNullOrWhiteSpace(InputText) ? PresetText : InputText;
        ActionText = string.IsNullOrWhiteSpace(text)
            ? "Isi kotak nilai (atau isi Preset di Pengaturan)."
            : SetFromText(text);
    }

    /// <summary>Hotkey Set: pakai Preset; kalau kosong pakai kotak nilai.</summary>
    public void HotkeySet()
    {
        string text = string.IsNullOrWhiteSpace(PresetText) ? InputText : PresetText;
        ActionText = string.IsNullOrWhiteSpace(text)
            ? "Hotkey Set: isi Preset di Pengaturan atau kotak nilai dulu."
            : SetFromText(text);
    }

    public void HotkeyToggleFreeze() => IsFrozen = !IsFrozen;

    /// <summary>Kunci otomatis di nilai Preset begitu nilai terbaca (sekali per proses game; kalau dilepas manual, tidak dikunci lagi).</summary>
    public bool AutoFreeze { get; set; }
    private int _autoPid;

    private void TryAutoFreeze(ProcessMemory? memory)
    {
        if (!AutoFreeze || memory is null || IsFrozen || _autoPid == memory.ProcessId) return;
        if (_lastRaw is null || string.IsNullOrWhiteSpace(PresetText)) return;

        _autoPid = memory.ProcessId;
        InputText = PresetText;
        IsFrozen = true;
    }

    private string SetFromText(string text) =>
        ValueCodec.TryParse(Saved.ValueType, text, out ulong raw)
            ? WriteOnce(raw)
            : "Isi nilai dengan angka yang valid.";

    /// <summary>Validasi, resolusi ulang alamat, tulis, baca ulang. Mengembalikan pesan hasil.</summary>
    private string WriteOnce(ulong raw)
    {
        var type = Saved.ValueType;

        if (!IsSane(raw)) return NotSane(raw);

        var memory = _session.Memory;
        var writer = _session.Writer;
        if (memory is null) return "Game belum terhubung.";
        if (writer is null)
            return _session.WriterError.Length > 0 ? _session.WriterError : "Izin menulis belum tersedia.";

        var result = AddressResolver.Resolve(memory, Chains, Saved.Signatures, type);
        if (result.Status != ResolveStatus.Ok)
            return "Alamat belum ditemukan di game ini, jadi tidak ada yang ditulis.";

        if (!IsSane(result.Raw))
            return $"Nilai sekarang di alamat ({ValueCodec.Format(type, result.Raw)}) di luar rentang aman ({RangeText}). " +
                   "Alamat kemungkinan salah, jadi tidak ada yang ditulis.";

        if (!writer.Write(result.Address, ToBytes(type, raw)))
            return "Windows menolak penulisan ke alamat itu (memori mungkin dilindungi).";

        if (IsFrozen) _frozenRaw = raw;

        int size = ValueCodec.SizeOf(type);
        var back = memory.Read(result.Address, size);
        if (back is null || ValueCodec.ReadRaw(back, 0, size) != raw)
            return "Nilai sudah ditulis, tetapi saat dibaca ulang berbeda. Game kemungkinan langsung menimpanya.";

        return $"Ditulis: {ValueCodec.Format(type, raw)}.";
    }

    // ---------- Freeze ----------

    partial void OnIsFrozenChanged(bool value)
    {
        _freezeHeld = false;

        if (!value)
        {
            ActionText = "Kunci dilepas.";
            return;
        }

        ulong raw;
        if (string.IsNullOrWhiteSpace(InputText))
        {
            if (_lastRaw is null)
            {
                Release("Nilai sekarang belum terbaca, jadi belum ada yang dikunci.");
                return;
            }

            raw = _lastRaw.Value;
            InputText = ValueCodec.Format(Saved.ValueType, raw);
        }
        else if (!ValueCodec.TryParse(Saved.ValueType, InputText, out raw))
        {
            Release("Isi kotak nilai dengan angka yang valid sebelum mengunci.");
            return;
        }

        if (!IsSane(raw))
        {
            Release(NotSane(raw));
            return;
        }

        if (_session.Memory is null || _session.Writer is null)
        {
            Release(_session.WriterError.Length > 0 ? _session.WriterError : "Game belum terhubung.");
            return;
        }

        _frozenRaw = raw;
        ActionText = $"Nilai dikunci di {ValueCodec.Format(Saved.ValueType, raw)}.";
    }

    /// <summary>Batalkan kunci (ditunda sedikit supaya tombol di layar ikut kembali mati).</summary>
    private void Release(string message)
    {
        ActionText = message;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            IsFrozen = false;
            ActionText = message;
        }));
    }

    private void Post(string message) =>
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => ActionText = message));

    /// <summary>Dipanggil thread latar (5 sampai 50 ms). Alamat di-resolve ulang tiap tick supaya tidak pernah menulis ke alamat basi.</summary>
    public void FreezeTick()
    {
        if (!IsFrozen) return;

        var memory = _session.Memory;
        var writer = _session.Writer;
        if (memory is null || writer is null) return;

        var result = AddressResolver.Resolve(memory, Chains, Saved.Signatures, Saved.ValueType);
        if (result.Status != ResolveStatus.Ok) return;

        if (!IsSane(result.Raw))
        {
            if (!_freezeHeld)
            {
                _freezeHeld = true;
                Post("Kunci ditahan: nilai di alamat ini di luar rentang aman (alamat mungkin berubah). Tidak menulis.");
            }
            return;
        }

        if (_freezeHeld)
        {
            _freezeHeld = false;
            Post($"Nilai dikunci di {ValueCodec.Format(Saved.ValueType, _frozenRaw)}.");
        }

        if (result.Raw != _frozenRaw)
            writer.Write(result.Address, ToBytes(Saved.ValueType, _frozenRaw));
    }

    // ---------- bantu ----------

    private static double ToDouble(ScanValueType type, ulong raw) => type switch
    {
        ScanValueType.Int32 => (int)(uint)raw,
        ScanValueType.Float => BitConverter.UInt32BitsToSingle((uint)raw),
        ScanValueType.Int64 => (long)raw,
        _ => BitConverter.UInt64BitsToDouble(raw)
    };

    private static byte[] ToBytes(ScanValueType type, ulong raw) =>
        ValueCodec.SizeOf(type) == 4 ? BitConverter.GetBytes((uint)raw) : BitConverter.GetBytes(raw);
}