using Microsoft.Extensions.DependencyInjection;
using Ramreader.App.Services;
using Ramreader.App.Views;
using Wpf.Ui.Controls;

namespace Ramreader.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = App.Services.GetRequiredService<GameSession>();
        RootNavigation.SetServiceProvider(App.Services);
        Loaded += (_, _) => RootNavigation.Navigate(typeof(ProcessPage));
    }
}