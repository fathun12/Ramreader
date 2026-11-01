using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Ramreader.Core.Debugging;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Trainer.Services;

namespace Ramreader.Trainer.ViewModels;

/// <summary>Satu code patch (God Mode) di trainer: cari lokasi lewat signature, cek byte, tulis, kembalikan.</summary>
public partial class PatchItem : ObservableObject
{
    private readonly CodePatch _patch;
    private readonly TrainerSession _session;
    private readonly object _lock = new();

    private ulong? _address;
    private int _pid;
    private int _searchedPid;
    private volatile bool _searching;
    private string _searchError = "";
    private bool _syncing;
    private bool _applyingSettings;

    public PatchItem(CodePatch patch, TrainerSession session)
    {
        _patch = patch;
        _session = session;
    }

    public string Label => _patch.Label;
    public string Title => string.IsNullOrWhiteSpace(_patch.Label) ? "God Mode" : _patch.Label;
    public string Detail => $"Mengganti instruksi penulis ({_patch.OriginalBytes}) dengan NOP, jadi nilai tidak pernah turun.";

    public event Action? SettingsChanged;

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private string _statusText = "Menunggu game...";
    [ObservableProperty] private string _hotkeyText = "";
    [ObservableProperty] private string _hotkeyStatus = "";

    private ulong? Address
    {
        get { lock (_lock) return _address; }
        set { lock (_lock) _address = value; }
    }

    // ---------- pengaturan ----------

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

    // ---------- membaca (timer 1 detik) ----------

    public void Update(ProcessMemory? memory)
    {
        if (memory is null)
        {
            Address = null;
            _pid = 0;
            _searchedPid = 0;
            StatusText = "Menunggu game...";
            SetOn(false);
            return;
        }

        if (memory.ProcessId != _pid)
        {
            _pid = memory.ProcessId;
            Address = null;
            _searchError = "";
        }

        var address = Address;
        if (address is null)
        {
            if (!_searching && _searchedPid != _pid) StartSearch(memory);

            StatusText = _searching || _searchedPid != _pid
                ? "Mencari lokasi kode lewat signature (sekali saja, bisa beberapa detik)..."
                : _searchError;
            return;
        }

        var state = CodePatchLocator.Inspect(memory, _patch, address.Value, out string detail);
        switch (state)
        {
            case PatchState.Original:
                StatusText = "Siap. Patch belum aktif.";
                SetOn(false);
                break;
            case PatchState.Patched:
                StatusText = "Patch aktif: nilai tidak akan turun.";
                SetOn(true);
                break;
            default:
                StatusText = detail;
                SetOn(false);
                break;
        }
    }

    private void StartSearch(ProcessMemory memory)
    {
        _searching = true;
        _searchedPid = memory.ProcessId;
        int pid = memory.ProcessId;

        Task.Run(() =>
        {
            try
            {
                var (address, message) = CodePatchLocator.Find(memory, _patch, CancellationToken.None);
                if (_pid == pid)
                {
                    _searchError = message;
                    Address = address;
                }
            }
            catch (Exception ex)
            {
                _searchError = "Gagal mencari lokasi kode: " + ex.Message;
            }
            finally
            {
                _searching = false;
            }
        });
    }

    // ---------- aktif / mati ----------

    partial void OnIsOnChanged(bool value)
    {
        if (_syncing) return;

        string error = Apply(value);
        if (error.Length == 0)
        {
            StatusText = value ? "Patch aktif: nilai tidak akan turun." : "Patch dimatikan.";
            return;
        }

        StatusText = error;
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            SetOn(!value);
            StatusText = error;
        }));
    }

    private string Apply(bool on)
    {
        var memory = _session.Memory;
        var writer = _session.Writer;
        if (memory is null) return "Game belum terhubung.";
        if (writer is null)
            return _session.WriterError.Length > 0 ? _session.WriterError : "Izin menulis belum tersedia.";

        var address = Address;
        if (address is null) return "Lokasi kode belum ditemukan. " + (_searchError.Length > 0 ? _searchError : "Tunggu pencarian selesai.");

        var state = CodePatchLocator.Inspect(memory, _patch, address.Value, out string detail);
        if (state is PatchState.Mismatch or PatchState.Unreadable)
            return detail + " Tidak ada yang ditulis.";

        if ((on && state == PatchState.Patched) || (!on && state == PatchState.Original)) return "";

        if (!CodePatch.TryParseBytes(on ? _patch.PatchBytes : _patch.OriginalBytes, out var bytes))
            return "Byte patch di profil rusak.";

        if (!writer.WriteCode(address.Value, bytes, out string error)) return error;

        var after = CodePatchLocator.Inspect(memory, _patch, address.Value, out _);
        return after == (on ? PatchState.Patched : PatchState.Original)
            ? ""
            : "Byte sudah ditulis, tetapi saat dibaca ulang berbeda. Game kemungkinan menimpanya.";
    }

    /// <summary>Kembalikan byte asli (dipanggil saat Trainer ditutup atau profil diganti).</summary>
    public void Restore()
    {
        try
        {
            var memory = _session.Memory;
            var writer = _session.Writer;
            var address = Address;
            if (memory is null || writer is null || address is null) return;

            if (CodePatchLocator.Inspect(memory, _patch, address.Value, out _) == PatchState.Patched &&
                CodePatch.TryParseBytes(_patch.OriginalBytes, out var original))
            {
                writer.WriteCode(address.Value, original, out _);
            }
        }
        catch
        {
            // Game sudah hilang: tidak ada yang perlu dikembalikan.
        }
        finally
        {
            SetOn(false);
        }
    }

    private void SetOn(bool value)
    {
        if (IsOn == value) return;
        _syncing = true;
        try { IsOn = value; }
        finally { _syncing = false; }
    }
}