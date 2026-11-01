using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ramreader.Trainer.Services;

namespace Ramreader.Trainer.ViewModels;

public sealed record FreezeSpeed(int Ms, string Label);

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private volatile FeatureItem[] _featureArray = Array.Empty<FeatureItem>();
    private volatile int _freezeIntervalMs = 10;
    private HotkeyService? _hotkeys;
    private bool _loadingSettings;

    public TrainerSession Session { get; } = new();
    public ObservableCollection<ProfileEntry> Profiles { get; } = new();
    public ObservableCollection<FeatureItem> Features { get; } = new();
    public ObservableCollection<PatchItem> Patches { get; } = new();

    /// <summary>Semua kartu di satu daftar (nilai dulu, lalu God Mode) supaya hanya ada satu area gulir.</summary>
    public ObservableCollection<object> Cards { get; } = new();

    public IReadOnlyList<FreezeSpeed> FreezeSpeeds { get; }

    [ObservableProperty]
    private ProfileEntry? _selectedProfile;

    [ObservableProperty]
    private FreezeSpeed _selectedSpeed;

    /// <summary>Fitur yang pengaturannya sedang tampil di panel samping (null = panel tertutup).</summary>
    [ObservableProperty]
    private FeatureItem? _settingsFeature;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVersionWarning))]
    private string _versionWarning = "";

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool HasVersionWarning => !string.IsNullOrEmpty(VersionWarning);
    public bool NoProfiles => Profiles.Count == 0;

    public MainViewModel()
    {
        FreezeSpeeds = new[]
        {
            new FreezeSpeed(50, "Hemat (50 ms)"),
            new FreezeSpeed(20, "Sedang (20 ms)"),
            new FreezeSpeed(10, "Cepat (10 ms)"),
            new FreezeSpeed(5, "Sangat cepat (5 ms)")
        };
        _selectedSpeed = FreezeSpeeds[2];

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        // Freeze: thread latar supaya tidak dibatasi resolusi timer UI (sekitar 15 ms).
        var thread = new Thread(FreezeLoop) { IsBackground = true, Name = "FreezeLoop" };
        thread.Start();

        RefreshProfiles();
    }

    // ---------- freeze (thread latar) ----------

    private void FreezeLoop()
    {
        timeBeginPeriod(1);
        try
        {
            while (true)
            {
                try
                {
                    foreach (var feature in _featureArray)
                        feature.FreezeTick();
                }
                catch
                {
                    // Satu tick gagal (mis. game baru ditutup): lanjut ke tick berikutnya.
                }

                Thread.Sleep(_freezeIntervalMs);
            }
        }
        finally
        {
            timeEndPeriod(1);
        }
    }

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);

    // ---------- hotkey ----------

    public void AttachHotkeys(HotkeyService hotkeys)
    {
        _hotkeys = hotkeys;
        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        var hotkeys = _hotkeys;
        if (hotkeys is null) return;

        hotkeys.UnregisterAll();

        foreach (var feature in Features)
        {
            var problems = new List<string>();
            RegisterOne(hotkeys, feature.HotkeySetText, feature.HotkeySet, "Set", problems);
            RegisterOne(hotkeys, feature.HotkeyFreezeText, feature.HotkeyToggleFreeze, "Kunci", problems);
            feature.HotkeyStatus = string.Join("  •  ", problems);
        }

        foreach (var patch in Patches)
        {
            var problems = new List<string>();
            RegisterOne(hotkeys, patch.HotkeyText, patch.HotkeyToggle, "God Mode", problems);
            patch.HotkeyStatus = string.Join("  •  ", problems);
        }
    }

    private static void RegisterOne(HotkeyService hotkeys, string text, Action action, string name, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        string? error = hotkeys.Register(text, action);
        if (error is not null) problems.Add($"Hotkey {name}: {error}");
    }

    // ---------- pengaturan ----------

    partial void OnSelectedSpeedChanged(FreezeSpeed value)
    {
        _freezeIntervalMs = value.Ms;
        SaveSettings();
    }

    private void OnFeatureSettingsChanged()
    {
        SaveSettings();
        RegisterHotkeys();
    }

    private void OnSettingsRequested(FeatureItem feature) => SettingsFeature = feature;

    [RelayCommand]
    private void CloseSettings() => SettingsFeature = null;

    private void SaveSettings()
    {
        var profile = SelectedProfile;
        if (profile is null || _loadingSettings) return;

        var settings = new GameSettings { FreezeIntervalMs = SelectedSpeed.Ms };
        foreach (var feature in Features)
            settings.Features[feature.Label] = feature.ToSettings();
        foreach (var patch in Patches)
            settings.Patches[patch.Label] = patch.ToSettings();

        try
        {
            SettingsStore.Save(profile.GameName, settings);
        }
        catch (Exception ex)
        {
            Message = "Gagal menyimpan pengaturan: " + ex.Message;
        }
    }

    // ---------- profil ----------

    partial void OnSelectedProfileChanged(ProfileEntry? value)
    {
        RestorePatches();

        foreach (var old in Features)
        {
            old.SettingsChanged -= OnFeatureSettingsChanged;
            old.SettingsRequested -= OnSettingsRequested;
        }
        foreach (var old in Patches)
            old.SettingsChanged -= OnFeatureSettingsChanged;

        SettingsFeature = null;
        Features.Clear();
        Patches.Clear();
        Cards.Clear();

        if (value is null)
        {
            _featureArray = Array.Empty<FeatureItem>();
            VersionWarning = "";
            _hotkeys?.UnregisterAll();
            return;
        }

        var settings = SettingsStore.Load(value.GameName);

        _loadingSettings = true;
        SelectedSpeed = FreezeSpeeds.FirstOrDefault(s => s.Ms == settings.FreezeIntervalMs) ?? FreezeSpeeds[2];
        _loadingSettings = false;

        foreach (var address in value.Addresses)
        {
            var item = new FeatureItem(address, Session);
            var stored = settings.Features.FirstOrDefault(
                kv => string.Equals(kv.Key, address.Label, StringComparison.OrdinalIgnoreCase)).Value;
            if (stored is not null) item.ApplySettings(stored);

            item.SettingsChanged += OnFeatureSettingsChanged;
            item.SettingsRequested += OnSettingsRequested;
            Features.Add(item);
            Cards.Add(item);
        }

        foreach (var codePatch in value.Addresses.SelectMany(a => a.CodePatches))
        {
            var patch = new PatchItem(codePatch, Session);
            if (settings.Patches.TryGetValue(codePatch.Label, out var storedPatch))
                patch.ApplySettings(storedPatch);

            patch.SettingsChanged += OnFeatureSettingsChanged;
            Patches.Add(patch);
            Cards.Add(patch);
        }

        _featureArray = Features.ToArray();

        // Pindah ke game lain: putuskan koneksi lama.
        if (Session.Memory is { } memory &&
            !memory.ProcessName.Equals(value.GameName, StringComparison.OrdinalIgnoreCase))
        {
            Session.Disconnect();
        }

        TryAutoConnect();
        RefreshVersionWarning();
        RegisterHotkeys();
    }

    /// <summary>Kembalikan semua code patch ke byte asli. Dipanggil saat profil diganti dan saat Trainer ditutup.</summary>
    private void RestorePatches()
    {
        foreach (var patch in Patches)
            patch.Restore();
    }

    public void Shutdown() => RestorePatches();

    [RelayCommand]
    private void RefreshProfiles()
    {
        string? keep = SelectedProfile?.DisplayName;
        var opened = Profiles.Where(p => p.Origin.Length > 0).ToList();

        Profiles.Clear();
        foreach (var profile in ProfileStore.LoadSessions()) Profiles.Add(profile);
        foreach (var profile in opened) Profiles.Add(profile);

        SelectedProfile = Profiles.FirstOrDefault(p => p.DisplayName == keep) ?? Profiles.FirstOrDefault();
        OnPropertyChanged(nameof(NoProfiles));
    }

    [RelayCommand]
    private void OpenProfile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Buka profil game",
            Filter = "Profil game (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        var entry = ProfileStore.LoadFile(dialog.FileName, Path.GetFileName(dialog.FileName));
        if (entry is null || entry.Addresses.Count == 0)
        {
            Message = "File ini bukan profil Ramreader yang valid, atau tidak berisi alamat.";
            return;
        }

        Message = "";
        var existing = Profiles.FirstOrDefault(p => p.DisplayName == entry.DisplayName);
        if (existing is not null) Profiles.Remove(existing);

        Profiles.Add(entry);
        SelectedProfile = entry;
        OnPropertyChanged(nameof(NoProfiles));
    }

    // ---------- timer 1 detik ----------

    private void OnTick()
    {
        if (SelectedProfile is null) return;

        if (!Session.IsConnected) TryAutoConnect();
        RefreshVersionWarning();

        var memory = Session.Memory;
        foreach (var feature in Features)
            feature.Update(memory);
        foreach (var patch in Patches)
            patch.Update(memory);
    }

    /// <summary>Cari proses game sesuai profil dan sambungkan kalau sudah berjalan.</summary>
    private void TryAutoConnect()
    {
        var profile = SelectedProfile;
        if (profile is null || Session.IsConnected) return;

        int? pid = null;
        foreach (var process in Process.GetProcessesByName(profile.GameName))
        {
            pid ??= process.Id;
            process.Dispose();
        }

        if (pid is null)
        {
            Session.StatusMessage = $"Menunggu {profile.GameName} dijalankan...";
            return;
        }

        try
        {
            Session.Connect(pid.Value);
            Message = "";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    /// <summary>Bandingkan ukuran exe sekarang dengan yang tercatat saat alamat disimpan.</summary>
    private void RefreshVersionWarning()
    {
        var main = Session.Memory?.MainModule;
        if (main is null || SelectedProfile is null)
        {
            VersionWarning = "";
            return;
        }

        bool mismatch = SelectedProfile.Addresses.Any(
            a => a.MainModuleSize != 0 && a.MainModuleSize != main.Size);

        VersionWarning = mismatch
            ? "Ukuran exe game berbeda dari saat profil dibuat. Game kemungkinan sudah di-update, alamat bisa tidak cocok."
            : "";
    }
}