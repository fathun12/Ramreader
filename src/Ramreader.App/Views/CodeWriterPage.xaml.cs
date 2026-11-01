using System.Windows.Controls;
using Ramreader.App.ViewModels;

namespace Ramreader.App.Views;

public partial class CodeWriterPage : Page
{
    public CodeWriterPage(CodeWriterViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}