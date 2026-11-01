using System.Windows.Controls;
using System.Windows.Input;
using Ramreader.App.ViewModels;

namespace Ramreader.App.Views;

public partial class ProcessPage : Page
{
    private readonly ProcessViewModel _vm;

    public ProcessPage(ProcessViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (_vm.Processes.Count == 0)
                _vm.RefreshCommand.Execute(null);
        };
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.ConnectCommand.CanExecute(null))
            _vm.ConnectCommand.Execute(null);
    }
}