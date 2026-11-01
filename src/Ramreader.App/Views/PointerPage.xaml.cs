using System.Windows.Controls;
using Ramreader.App.ViewModels;

namespace Ramreader.App.Views;

public partial class PointerPage : Page
{
    public PointerPage(PointerViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}