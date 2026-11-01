using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ramreader.App.Services;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;
using Ramreader.Core.Pointers;
using Ramreader.Core.Scanning;

namespace Ramreader.App.ViewModels;

/// <summary>Satu kartu di halaman Tersimpan. Nilainya dibaca langsung dari game.</summary>
public partial class SavedItem : ObservableObject
{
    public SavedItem(SavedAddress saved)
    {
        Saved = saved;
        foreach (var line in saved.Chains)
        {
            if (PointerChain.TryParse(line, out var chain))
                Chains.Add(chain);
        }
    }

    public SavedAddress Saved { get; }
    public List<PointerChain> Chains { get; } = new();

    public string Label => Saved.Label;
    public string ChainText => Chains.Count > 0 ? Chains[0].Describe() : "(tidak ada jalur)";

    public string DetailText =>
        $"{Chains.Count} jalur  •  {TypeName(Saved.ValueType)}  •  " +
        (Saved.VerifiedRounds > 0 ? $"terverifikasi {Saved.VerifiedRounds}x" : "belum diverifikasi") +
        (Saved.Signatures.Count > 0 ? $"  •  {Saved.Signatures.Count} signature" : "");

    /// <summary>Panggil setelah daftar signature berubah supaya teks detail kartu diperbarui.</summary>
    public void NotifyDetailChanged() => OnPropertyChanged(nameof(DetailText));

    [ObservableProperty]
    private string _valueText = "-";

    [ObservableProperty]
    private string _statusText = "";

    private static string TypeName(ScanValueType type) => type switch
    {
        ScanValueType.Int32 => "int",
        ScanValueType.Float => "float",
        ScanValueType.Int64 => "int64",
        _ => "double"
    };

    /// <summary>Coba tiap jalur berurutan sampai ada yang menghasilkan nilai yang terbaca.</summary>
    public void Update(ProcessMemory? memory)
    {
        if (memory is null)
        {
            ValueText = "-";
            StatusText = "Game belum terhubung. Hubungkan di menu Proses untuk melihat nilainya.";
            return;
        }

        var result = AddressResolver.Resolve(memory, Chains, Saved.Signatures, Saved.ValueType);

        switch (result.Status)
        {
            case ResolveStatus.Ok when result.Source == ResolveSource.Chain:
                ValueText = ValueCodec.Format(Saved.ValueType, result.Raw);
                StatusText = result.Index == 0
                    ? "Terbaca lewat jalur utama."
                    : $"Jalur utama gagal, terbaca lewat jalur cadangan #{result.Index + 1}.";
                return;

            case ResolveStatus.Ok:
                ValueText = ValueCodec.Format(Saved.ValueType, result.Raw);
                StatusText = $"Semua jalur putus, terbaca lewat signature #{result.Index + 1}. " +
                             "Cari jalur baru lalu simpan lagi supaya jalur ikut diperbarui.";
                return;

            case ResolveStatus.SignatureSearching:
                ValueText = "...";
                StatusText = "Semua jalur putus. Mencari lewat signature (sekali saja, bisa beberapa detik)...";
                return;
        }

        ValueText = "?";
        StatusText = "Tidak ada jalur atau signature yang bisa dibaca di game sekarang. Mungkin game belum sampai layar yang memuat nilai ini, atau game sudah di-update.";
    }
}

public partial class SavedViewModel : ObservableObject
{
    private readonly SavedStore _store;
    private readonly DispatcherTimer _timer;
    private string? _processName;

    public GameSession Session { get; }

    public ObservableCollection<SavedItem> Items { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyChainCommand))]
    private SavedItem? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool NotConnected => !Session.IsConnected;
    public bool IsEmpty => Items.Count == 0;
    public string GameText => _processName is null ? "Belum ada game dipilih" : $"Game: {_processName}";
    public string CountText => Items.Count == 0 ? "Belum ada alamat tersimpan" : $"{Items.Count:N0} alamat tersimpan";

    public SavedViewModel(GameSession session, SavedStore store)
    {
        Session = session;
        _store = store;

        Session.PropertyChanged += OnSessionChanged;

        // Perbarui nilai tiap detik supaya kartu menunjukkan nilai terbaru dari game.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(GameSession.Memory) or nameof(GameSession.IsConnected)))
            return;

        OnPropertyChanged(nameof(NotConnected));

        var memory = Session.Memory;
        if (memory is not null &&
            !string.Equals(_processName, memory.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            Load(memory.ProcessName);
        }

        Refresh();
    }

    // ---------- dipanggil dari halaman Jalur Stabil ----------

    /// <summary>Tambah alamat baru. Label yang sama (tanpa peduli huruf besar/kecil) menggantikan yang lama.</summary>
    public void Add(SavedAddress saved)
    {
        var memory = Session.Memory;
        if (memory is null) return;

        if (!string.Equals(_processName, memory.ProcessName, StringComparison.OrdinalIgnoreCase))
            Load(memory.ProcessName);

        var existing = Items.FirstOrDefault(
            i => string.Equals(i.Label, saved.Label, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) Items.Remove(existing);

        var item = new SavedItem(saved);
        Items.Add(item);
        item.Update(memory);

        Persist();
        NotifyList();
    }

    /// <summary>Tambah signature ke alamat tersimpan. Pola yang sama menggantikan yang lama; maksimal 5 per alamat.</summary>
    public void AttachSignature(SavedItem item, SignatureRef signature)
    {
        var list = item.Saved.Signatures;
        list.RemoveAll(s => string.Equals(s.Pattern, signature.Pattern, StringComparison.Ordinal));
        list.Add(signature);
        while (list.Count > 5) list.RemoveAt(0);

        item.NotifyDetailChanged();
        Persist();
    }

    // ---------- perintah ----------

    private bool HasSelection() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        var item = SelectedItem;
        if (item is null) return;

        Items.Remove(item);
        SelectedItem = null;
        Persist();
        NotifyList();
        Message = $"\"{item.Label}\" dihapus.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyChain()
    {
        if (SelectedItem is null) return;

        try
        {
            Clipboard.SetText(SelectedItem.ChainText);
            Message = "Jalur utama disalin.";
        }
        catch (Exception ex)
        {
            Message = "Gagal menyalin: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Export()
    {
        if (Items.Count == 0 || _processName is null)
        {
            Message = "Belum ada alamat tersimpan untuk diekspor.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Ekspor profil game",
            FileName = $"{_processName}.profile.json",
            Filter = "Profil game (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var profile = new GameProfile
            {
                GameName = _processName,
                ExportedAt = DateTime.Now,
                Addresses = Items.Select(i => i.Saved).ToList()
            };
            _store.Export(dialog.FileName, profile);
            Message = $"Profil diekspor ke {dialog.FileName}";
        }
        catch (Exception ex)
        {
            Message = "Gagal mengekspor: " + ex.Message;
        }
    }

    // ---------- logika ----------

    private void Load(string processName)
    {
        Items.Clear();
        foreach (var saved in _store.Load(processName))
            Items.Add(new SavedItem(saved));

        _processName = processName;
        SelectedItem = null;
        NotifyList();
    }

    private void Persist()
    {
        if (_processName is null) return;

        try
        {
            _store.Save(_processName, Items.Select(i => i.Saved).ToList());
        }
        catch (Exception ex)
        {
            Message = "Gagal menyimpan ke disk: " + ex.Message;
        }
    }

    private void Refresh()
    {
        if (Items.Count == 0) return;

        var memory = Session.Memory;
        foreach (var item in Items)
            item.Update(memory);
    }

    private void NotifyList()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(GameText));
    }
}