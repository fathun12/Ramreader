using CommunityToolkit.Mvvm.ComponentModel;

namespace Ramreader.App.ViewModels;

/// <summary>Satu baris hasil scan di daftar. Nilainya diperbarui langsung saat game berjalan.</summary>
public partial class ScanRow : ObservableObject
{
    public ScanRow(ulong address)
    {
        Address = address;
    }

    public ulong Address { get; }
    public string AddressText => $"0x{Address:X}";

    [ObservableProperty]
    private string _valueText = "";

    [ObservableProperty]
    private string _previousText = "";
}