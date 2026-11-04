using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Ramreader.Trainer.Services;

namespace Ramreader.Trainer.ViewModels;

/// <summary>
/// Satu tombol "Awakening Instan": matikan God Mode, set HP 40 lalu kunci, nyalakan patch durasi Awakening (sisi).
/// Pemicu Awakening di game tetap ditekan user sendiri.
/// </summary>
public partial class InstantAwakeningItem : ObservableObject
{
    public const string CardLabel = "Awakening Instan";
    private const string HpLabel = "HP";
    private const string GodModeLabel = "God Mode";
    private const string DurationLabel = "Awakening Durasi Tanpa Batas (sisi)";
    private const string HpValue = "40";

    private readonly ObservableCollection<FeatureItem> _features;
    private readonly ObservableCollection<PatchItem> _patches;
    private bool _applyingSettings;

    public InstantAwakeningItem(ObservableCollection<FeatureItem> features, ObservableCollection<PatchItem> patches)
    {
        _features = features;
        _patches = patches;
    }

    public static bool CanBuild(IEnumerable<FeatureItem> features, IEnumerable<PatchItem> patches) =>
        FindHp(features) is not null && FindPatch(patches, DurationLabel) is not null;

    private static FeatureItem? FindHp(IEnumerable<FeatureItem> features) =>
        features.FirstOrDefault(f => string.Equals(f.Label, HpLabel, StringComparison.OrdinalIgnoreCase));

    private static PatchItem? FindPatch(IEnumerable<PatchItem> patches, string label) =>
        patches.FirstOrDefault(p => string.Equals(p.Label, label, StringComparison.OrdinalIgnoreCase));

    public string Title => CardLabel;

    public string Detail =>
        "Satu tombol: God Mode dimatikan, HP diset 40 dan dikunci, lalu durasi Awakening tanpa batas dinyalakan. " +
        "Pemicu Awakening tetap kamu tekan sendiri. Kunci HP tidak mencegah HP turun, hanya mengembalikannya.";

    public event Action? SettingsChanged;

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private string _statusText = "Siap.";
    [ObservableProperty] private string _hotkeyText = "";
    [ObservableProperty] private string _hotkeyStatus = "";

    // ---------- pengaturan (hotkey) ----------

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_applyingSettings && e.PropertyName == nameof(HotkeyText))
            SettingsChanged?.Invoke();
    }

    public PatchSettings ToSettings() => new() { Hotkey = HotkeyText };

    public void ApplySettings(PatchSettings s)
    {
        _applyingSettings = true;
        try { HotkeyText = s.Hotkey ?? ""; }
        finally { _applyingSettings = false; }
    }

    public void HotkeyToggle() => IsOn = !IsOn;

    // ---------- nyala / mati ----------

    partial void OnIsOnChanged(bool value)
    {
        StatusText = value ? Start() : Stop();
    }

    private string Start()
    {
        var hp = FindHp(_features);
        var duration = FindPatch(_patches, DurationLabel);
        if (hp is null || duration is null)
            return "Kartu HP atau patch durasi tidak ada di profil ini.";

        var god = FindPatch(_patches, GodModeLabel);
        if (god is { IsOn: true }) god.IsOn = false;

        if (hp.IsFrozen) hp.IsFrozen = false;
        hp.InputText = HpValue;
        hp.IsFrozen = true;

        duration.IsOn = true;

        return $"God Mode: {(god is null ? "tidak ada" : god.StatusText)} | HP: {hp.ActionText} | Durasi: {duration.StatusText}";
    }

    private string Stop()
    {
        var hp = FindHp(_features);
        var duration = FindPatch(_patches, DurationLabel);

        if (hp is not null)
        {
            if (hp.IsFrozen) hp.IsFrozen = false;
            if (hp.InputText == HpValue) hp.InputText = "";
        }
        if (duration is not null && duration.IsOn) duration.IsOn = false;

        return "Dimatikan: kunci HP dilepas dan patch durasi mati. God Mode tidak dinyalakan otomatis.";
    }

    /// <summary>Dipanggil timer 1 detik: kalau kartu menyala tetapi kunci HP atau patch durasi sudah mati/gagal, lepas semuanya.</summary>
    public void Refresh()
    {
        if (!IsOn) return;

        var hp = FindHp(_features);
        var duration = FindPatch(_patches, DurationLabel);
        bool ok = hp is { IsFrozen: true } && duration is { IsOn: true };
        if (ok) return;

        string before = StatusText;
        IsOn = false;
        StatusText = "Dilepas otomatis karena ada bagian yang mati atau gagal. Sebelumnya: " + before;
    }
}