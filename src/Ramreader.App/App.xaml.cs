using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Ramreader.App.Services;
using Ramreader.App.ViewModels;
using Ramreader.App.Views;

namespace Ramreader.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        services.AddSingleton<GameSession>();
        services.AddSingleton<ProcessViewModel>();
        services.AddSingleton<ScanViewModel>();
        services.AddSingleton<PointerViewModel>();
        services.AddSingleton<ChainStore>();
        services.AddSingleton<SavedStore>();
        services.AddSingleton<SavedViewModel>();
        services.AddSingleton<SignatureViewModel>();
        services.AddSingleton<CodeWriterViewModel>();
        services.AddSingleton<CharacterObjectsViewModel>();

        services.AddSingleton<ProcessPage>();
        services.AddSingleton<ScanPage>();
        services.AddSingleton<PointerPage>();
        services.AddSingleton<SignaturePage>();
        services.AddSingleton<CodeWriterPage>();
        services.AddSingleton<CharacterObjectsPage>();
        services.AddSingleton<SavedPage>();

        Services = services.BuildServiceProvider();

        base.OnStartup(e);
    }
}