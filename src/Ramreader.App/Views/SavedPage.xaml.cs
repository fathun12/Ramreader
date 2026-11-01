using System.Windows.Controls;
using Ramreader.App.ViewModels;

namespace Ramreader.App.Views;

public partial class SavedPage : Page
{
    public SavedPage(SavedViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}