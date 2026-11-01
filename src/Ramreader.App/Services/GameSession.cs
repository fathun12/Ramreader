using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;

namespace Ramreader.App.Services;

/// <summary>
/// Menyimpan koneksi ke game yang sedang aktif. Dipakai bersama oleh semua halaman
/// dan oleh bilah status di bawah jendela.
/// </summary>
public partial class GameSession : ObservableObject
{
    private const string IdleMessage = "Belum terhubung ke game. Mulai dari menu Proses.";

    private readonly DispatcherTimer _watchdog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    private ProcessMemory? _memory;

    [ObservableProperty]
    private string _statusMessage = IdleMessage;

    /// <summary>Alamat yang dipilih user sebagai target pencarian jalur stabil (null = belum dipilih).</summary>
    [ObservableProperty]
    private ulong? _targetAddress;

    /// <summary>Tipe nilai alamat target (int, float, ...), diisi saat Jadikan Target.</summary>
    [ObservableProperty]
    private ScanValueType? _targetType;

    public bool IsConnected => Memory is not null;

    public GameSession()
    {
        // Cek tiap 1,5 detik apakah game masih hidup.
        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _watchdog.Tick += (_, _) =>
        {
            if (Memory is not null && !Memory.IsAlive)
                Disconnect("Game ditutup. Buka lagi game-nya, lalu hubungkan ulang di menu Proses.");
        };
        _watchdog.Start();
    }

    /// <summary>Melempar InvalidOperationException (pesan sudah ramah) kalau gagal.</summary>
    public void Connect(int processId)
    {
        Disconnect();

        var memory = ProcessMemory.Attach(processId);
        Memory = memory;
        StatusMessage = $"Terhubung ke {memory.ProcessName} (PID {memory.ProcessId}). Langkah berikutnya: menu Cari Nilai.";
    }

    public void Disconnect(string? message = null)
    {
        Memory?.Dispose();
        Memory = null;
        TargetAddress = null;
        TargetType = null;
        StatusMessage = message ?? IdleMessage;
    }
}