using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Ramreader.Core.Debugging;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Core.Pointers;
using Ramreader.Trainer.Services;

namespace Ramreader.Trainer.ViewModels;

/// <summary>
/// Satu code patch di trainer. Mode bawaan: code cave "pemain saja" (musuh tetap bisa dikalahkan).
/// Mode "Nop": NOP polos (semua karakter yang memakai instruksi itu ikut kebal).
/// </summary>
public partial class PatchItem : ObservableObject
{
    private readonly CodePatch _patch;
    private readonly SavedAddress _owner;
    private readonly List<PointerChain> _chains = new();
    private readonly TrainerSession _session;
    private readonly object _lock = new();
    private readonly bool _playerOnly;
    private readonly CaveInfo? _cave;
    private readonly string _caveError = "";

    private ulong? _address;
    private ulong _caveAddr;
    private int _slotOffset;
    private int _dataOffset;
    private int[] _immOffsets = Array.Empty<int>();
    private uint[]? _lastMarkers;

    // Penanda sisi = 3 dword berurutan di objek karakter. Satu dword saja bisa kebetulan sama antar sisi.
    private const int MarkerStart = 0x3C;
    private const int MarkerWords = 3;
    private ulong _lastSlot;
    private int _pid;
    private int _searchedPid;
    private volatile bool _searching;
    private volatile bool _active;
    private string _searchError = "";
    private bool _syncing;
    private bool _applyingSettings;

    public PatchItem(CodePatch patch, SavedAddress owner, TrainerSession session)
    {
        _patch = patch;
        _owner = owner;
        _session = session;

        foreach (var line in owner.Chains)
        {
            if (PointerChain.TryParse(line, out var chain)) _chains.Add(chain);
        }

        _playerOnly = !string.Equals(patch.Mode, "Nop", StringComparison.OrdinalIgnoreCase);
        if (_playerOnly)
        {
            string error = "";
            if (CodePatch.TryParseBytes(patch.OriginalBytes, out var original) &&
                CodeCave.TryAnalyze(original, out var info, out error))
                _cave = info;
            else
                _caveError = error.Length > 0 ? error : "Byte patch di profil rusak.";
        }
    }

    public string Label => _patch.Label;
    public string Title => string.IsNullOrWhiteSpace(_patch.Label) ? "God Mode" : _patch.Label;

    private bool IsTeam => string.Equals(_patch.Mode, "Team", StringComparison.OrdinalIgnoreCase);

    public string Detail => IsTeam
        ? $"Seluruh timmu kebal (dikenali dari penanda di objek); musuh tetap bisa dikalahkan. Mengganti instruksi penulis ({_patch.OriginalBytes})."
        : _playerOnly
        ? $"Hanya karakter pemain yang kebal; musuh tetap bisa dikalahkan. Mengganti instruksi penulis ({_patch.OriginalBytes})."
        : $"NOP polos ({_patch.OriginalBytes}): semua karakter yang memakai instruksi ini ikut kebal.";

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

    // ---------- thread latar: menjaga slot "siapa pemain" tetap benar ----------

    public void Tick()
    {
        if (!_active || _cave is null) return;

        var memory = _session.Memory;
        var writer = _session.Writer;
        if (memory is null || writer is null) return;

        ulong cave, last;
        int slotOffset;
        lock (_lock)
        {
            cave = _caveAddr;
            slotOffset = _slotOffset;
            last = _lastSlot;
        }
        if (cave == 0) return;

        if (IsTeam)
        {
            var markers = ComputeMarkers(memory);
            if (markers is null) return;

            uint[]? prev;
            int[] imms;
            lock (_lock) { prev = _lastMarkers; imms = _immOffsets; }
            if (imms.Length != markers.Length) return;
            if (prev is not null && prev.AsSpan().SequenceEqual(markers)) return;

            bool ok = true;
            for (int i = 0; i < markers.Length; i++)
                ok &= writer.Write(cave + (ulong)imms[i], BitConverter.GetBytes(markers[i]));
            if (ok) lock (_lock) _lastMarkers = markers;
            return;
        }

        var slot = ComputeSlot(memory);
        if (slot is null || slot.Value == last) return;

        if (writer.Write(cave + (ulong)slotOffset, BitConverter.GetBytes(slot.Value)))
        {
            lock (_lock) _lastSlot = slot.Value;
        }
    }

    /// <summary>Nilai register basis milik pemain = alamat nilai pemain dikurangi displacement instruksi.</summary>
    private ulong? ComputeSlot(ProcessMemory memory)
    {
        var info = _cave;
        if (info is null) return null;

        var result = AddressResolver.Resolve(memory, _chains, _owner.Signatures, _owner.ValueType);
        return result.Status == ResolveStatus.Ok
            ? unchecked(result.Address - (ulong)info.Disp)
            : null;
    }

    /// <summary>Penanda tim milik pemain: 3 dword di [objek pemain + 0x3C]. Semua nol dianggap belum valid.</summary>
    private uint[]? ComputeMarkers(ProcessMemory memory)
    {
        var playerBase = ComputeSlot(memory);
        if (playerBase is null) return null;

        var bytes = memory.Read(unchecked(playerBase.Value + (ulong)MarkerStart), MarkerWords * 4);
        if (bytes is null) return null;

        var result = new uint[MarkerWords];
        for (int i = 0; i < MarkerWords; i++) result[i] = BitConverter.ToUInt32(bytes, i * 4);
        return result.All(v => v == 0) ? null : result;
    }

    /// <summary>Diagnostik mode tim: berapa kali cave melewati penulisan (dianggap satu tim) dan menjalankannya (dianggap musuh).</summary>
    private string Diagnostic(ProcessMemory memory)
    {
        ulong cave;
        int data;
        lock (_lock) { cave = _caveAddr; data = _dataOffset; }
        if (cave == 0 || data == 0) return "";

        var bytes = memory.Read(cave + (ulong)data, 24);
        if (bytes is null) return " | Diagnostik: belum terbaca";

        uint skip = BitConverter.ToUInt32(bytes, 0);
        uint run = BitConverter.ToUInt32(bytes, 4);
        ulong lastSkip = BitConverter.ToUInt64(bytes, 8);
        ulong lastRun = BitConverter.ToUInt64(bytes, 16);

        string MarkerOf(ulong obj)
        {
            var b = obj != 0 ? memory.Read(obj + (ulong)MarkerStart, MarkerWords * 4) : null;
            return b is null ? "-" : string.Join(" ", Enumerable.Range(0, MarkerWords).Select(i => BitConverter.ToUInt32(b, i * 4).ToString("X8")));
        }

        return $" | Diagnostik: dilewati {skip}x, dijalankan {run}x. " +
               $"Objek terakhir dilewati 0x{lastSkip:X} (penanda {MarkerOf(lastSkip)}), " +
               $"objek terakhir dijalankan 0x{lastRun:X} (penanda {MarkerOf(lastRun)}).";
    }

    // ---------- membaca (timer 1 detik) ----------

    public void Update(ProcessMemory? memory)
    {
        if (memory is null)
        {
            Address = null;
            _pid = 0;
            _searchedPid = 0;
            _active = false;
            lock (_lock) { _caveAddr = 0; _lastSlot = 0; }
            StatusText = "Menunggu game...";
            SetOn(false);
            return;
        }

        if (memory.ProcessId != _pid)
        {
            _pid = memory.ProcessId;
            Address = null;
            _active = false;
            lock (_lock) { _caveAddr = 0; _lastSlot = 0; }
            _searchError = "";
        }

        if (_playerOnly && _cave is null)
        {
            StatusText = "Patch ini tidak bisa dibuat mode pemain-saja: " + _caveError;
            SetOn(false);
            return;
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

        var state = Inspect(memory, address.Value, out string detail);
        switch (state)
        {
            case PatchState.Original:
                _active = false;
                StatusText = "Siap. Patch belum aktif.";
                SetOn(false);
                break;
            case PatchState.Patched:
                _active = true;
                StatusText = IsTeam
                    ? "Patch aktif (mode tim): seluruh timmu tidak berkurang, musuh tetap. Penanda pemain: " +
                      (ComputeMarkers(memory) is { } pm ? string.Join(" ", pm.Select(v => v.ToString("X8"))) : "belum terbaca") + Diagnostic(memory)
                    : _playerOnly
                        ? "Patch aktif: hanya karakter pemain yang tidak berkurang."
                        : "Patch aktif (NOP polos): semua karakter ikut tidak berkurang.";
                SetOn(true);
                break;
            default:
                _active = false;
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

    // ---------- periksa byte di lokasi kode ----------

    private PatchState Inspect(ProcessMemory memory, ulong address, out string detail)
    {
        if (!_playerOnly) return CodePatchLocator.Inspect(memory, _patch, address, out detail);

        detail = "";
        if (!CodePatch.TryParseBytes(_patch.OriginalBytes, out var original))
        {
            detail = "Data patch di profil rusak.";
            return PatchState.Unreadable;
        }

        var now = memory.Read(address, original.Length);
        if (now is null)
        {
            detail = "Kode di lokasi itu belum terbaca.";
            return PatchState.Unreadable;
        }

        if (now.AsSpan().SequenceEqual(original)) return PatchState.Original;

        ulong cave;
        lock (_lock) cave = _caveAddr;
        if (now[0] == 0xE9 && cave != 0)
        {
            int rel = BitConverter.ToInt32(now, 1);
            ulong target = unchecked(address + 5UL + (ulong)(long)rel);
            if (target == cave) return PatchState.Patched;
        }

        detail = $"Byte di lokasi kode berbeda (ada {CodePatch.ToHex(now)}, seharusnya {CodePatch.ToHex(original)}). " +
                 "Game sudah di-update, atau patch dari sesi Trainer sebelumnya masih terpasang (kalau Trainer ditutup paksa, restart game).";
        return PatchState.Mismatch;
    }

    // ---------- aktif / mati ----------

    partial void OnIsOnChanged(bool value)
    {
        if (_syncing) return;

        string error = Apply(value);
        if (error.Length == 0)
        {
            StatusText = value
                ? (_playerOnly ? "Patch aktif: hanya karakter pemain yang tidak berkurang." : "Patch aktif (NOP polos).")
                : "Patch dimatikan.";
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
        if (address is null)
            return "Lokasi kode belum ditemukan. " + (_searchError.Length > 0 ? _searchError : "Tunggu pencarian selesai.");

        var state = Inspect(memory, address.Value, out string detail);
        if (state is PatchState.Mismatch or PatchState.Unreadable)
            return detail + " Tidak ada yang ditulis.";

        if ((on && state == PatchState.Patched) || (!on && state == PatchState.Original))
        {
            _active = on;
            return "";
        }

        if (!CodePatch.TryParseBytes(_patch.OriginalBytes, out var original))
            return "Byte patch di profil rusak.";

        byte[] bytes;
        if (!on)
        {
            bytes = original;
        }
        else if (_playerOnly)
        {
            string prepared = PrepareCave(memory, writer, address.Value, original, out bytes);
            if (prepared.Length > 0) return prepared;
        }
        else if (!CodePatch.TryParseBytes(_patch.PatchBytes, out bytes))
        {
            return "Byte patch di profil rusak.";
        }

        if (!writer.WriteCode(address.Value, bytes, out string error)) return error;

        var after = Inspect(memory, address.Value, out _);
        if (after != (on ? PatchState.Patched : PatchState.Original))
            return "Byte sudah ditulis, tetapi saat dibaca ulang berbeda. Game kemungkinan menimpanya.";

        _active = on;
        return "";
    }

    /// <summary>Siapkan cave di memori game dan kembalikan byte lompatan yang akan menimpa instruksi penulis.</summary>
    private string PrepareCave(ProcessMemory memory, MemoryWriter writer, ulong site, byte[] original, out byte[] jump)
    {
        jump = Array.Empty<byte>();

        var info = _cave;
        if (info is null)
            return "Patch ini tidak bisa dibuat mode pemain-saja: " + _caveError;

        var slotValue = ComputeSlot(memory);
        if (slotValue is null)
            return "Alamat nilai milik pemain belum ditemukan. Masuk ke match dulu, lalu nyalakan lagi.";

        ulong cave;
        lock (_lock) cave = _caveAddr;

        if (cave == 0)
        {
            var allocated = writer.AllocNear(site, 0x1000);
            if (allocated is null) return "Gagal menyediakan memori dekat kode game untuk patch.";
            cave = allocated.Value;
        }

        byte[] code;
        int slotOffset;
        ulong slotStore = slotValue.Value;
        uint[]? teamMarkers = null;
        int[] immOffsets = Array.Empty<int>();
        if (IsTeam)
        {
            teamMarkers = ComputeMarkers(memory);
            if (teamMarkers is null)
                return "Penanda tim milik pemain belum terbaca (objek pemain belum siap). Masuk ke match, gerakkan karakter, lalu nyalakan lagi.";

            code = CodeCave.BuildTeamDiagMulti(original, info, MarkerStart, teamMarkers, cave, site, out immOffsets, out int dataOff);
            slotOffset = 0;
            lock (_lock) _dataOffset = dataOff;
        }
        else
        {
            code = CodeCave.Build(original, info, cave, site, out slotOffset);
            BitConverter.GetBytes(slotValue.Value).CopyTo(code, slotOffset);
        }
        if (!writer.Write(cave, code)) return "Gagal menulis kode patch ke memori game.";

        long rel = (long)cave - (long)(site + 5);
        if (rel > int.MaxValue || rel < int.MinValue) return "Memori patch terlalu jauh dari kode game.";

        jump = new byte[original.Length];
        Array.Fill(jump, (byte)0x90);
        jump[0] = 0xE9;
        BitConverter.GetBytes((int)rel).CopyTo(jump, 1);

        lock (_lock)
        {
            _caveAddr = cave;
            _slotOffset = slotOffset;
            _lastSlot = slotStore;
            _immOffsets = immOffsets;
            _lastMarkers = teamMarkers;
        }
        return "";
    }

    /// <summary>Kembalikan byte asli (saat Trainer ditutup atau profil diganti). Cave dibiarkan, tidak berbahaya.</summary>
    public void Restore()
    {
        try
        {
            _active = false;

            var memory = _session.Memory;
            var writer = _session.Writer;
            var address = Address;
            if (memory is null || writer is null || address is null) return;

            if (Inspect(memory, address.Value, out _) == PatchState.Patched &&
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