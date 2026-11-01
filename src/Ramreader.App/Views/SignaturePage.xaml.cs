using System.Windows.Controls;
using Ramreader.App.ViewModels;

namespace Ramreader.App.Views;

public partial class SignaturePage : Page
{
    public SignaturePage(SignatureViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}