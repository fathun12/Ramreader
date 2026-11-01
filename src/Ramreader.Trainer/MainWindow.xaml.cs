using Ramreader.Trainer.Services;
using Ramreader.Trainer.ViewModels;
using Wpf.Ui.Controls;

namespace Ramreader.Trainer;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm = new();
    private HotkeyService? _hotkeys;

    public MainWindow()
    {
        DataContext = _vm;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hotkeys = new HotkeyService(this);
        _vm.AttachHotkeys(_hotkeys);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.Shutdown();
        _hotkeys?.Dispose();
        base.OnClosed(e);
    }
}