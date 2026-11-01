using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Ramreader.Core.Memory;

namespace Ramreader.Trainer.Services;

/// <summary>Koneksi (read) ke game yang sedang aktif. Memutus otomatis kalau game ditutup.</summary>
public partial class TrainerSession : ObservableObject
{
    private const string IdleMessage = "Pilih game di atas. Trainer menyambung otomatis begitu game berjalan.";

    private readonly DispatcherTimer _watchdog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    private ProcessMemory? _memory;

    [ObservableProperty]
    private string _statusMessage = IdleMessage;

    public bool IsConnected => Memory is not null;

    /// <summary>Penulis memori (izin tulis). Null kalau izin ditolak; alasannya ada di WriterError.</summary>
    public MemoryWriter? Writer { get; private set; }

    [ObservableProperty]
    private string _writerError = "";

    public TrainerSession()
    {
        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _watchdog.Tick += (_, _) =>
        {
            if (Memory is not null && !Memory.IsAlive)
                Disconnect("Game ditutup. Buka lagi game-nya, trainer akan menyambung otomatis.");
        };
        _watchdog.Start();
    }

    /// <summary>Melempar InvalidOperationException (pesan sudah ramah) kalau gagal.</summary>
    public void Connect(int processId)
    {
        Disconnect();

        var memory = ProcessMemory.Attach(processId);
        Memory = memory;

        try
        {
            Writer = MemoryWriter.Open(processId);
            WriterError = "";
        }
        catch (InvalidOperationException ex)
        {
            Writer = null;
            WriterError = ex.Message;
        }
        StatusMessage = $"Terhubung ke {memory.ProcessName} (PID {memory.ProcessId}).";
    }

    public void Disconnect(string? message = null)
    {
        Memory?.Dispose();
        Memory = null;
        Writer?.Dispose();
        Writer = null;
        WriterError = "";
        StatusMessage = message ?? IdleMessage;
    }
}