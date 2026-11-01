using System.Windows.Controls;
using Ramreader.App.ViewModels;

namespace Ramreader.App.Views;

public partial class ScanPage : Page
{
    public ScanPage(ScanViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}