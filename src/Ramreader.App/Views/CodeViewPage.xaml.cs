using System.Windows.Controls;
using Ramreader.App.ViewModels;

namespace Ramreader.App.Views;

public partial class CodeViewPage : Page
{
    public CodeViewPage(CodeViewViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}