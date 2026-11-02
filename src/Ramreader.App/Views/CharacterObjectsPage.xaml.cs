using System.Windows.Controls;
using Ramreader.App.ViewModels;

namespace Ramreader.App.Views;

public partial class CharacterObjectsPage : Page
{
    public CharacterObjectsPage(CharacterObjectsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}