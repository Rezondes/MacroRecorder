using System.Windows;
using MacroRecorder.App.Localization;
using MacroRecorder.App.Services;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MacroRecorder.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs startupEventArgs)
    {
        base.OnStartup(startupEventArgs);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddMacroRecorderInfrastructure();
        builder.Services.AddSingleton<MacroWorkspaceService>();
        builder.Services.AddSingleton<RecordingCoordinator>();
        builder.Services.AddSingleton<IUiLocalizer, ResxUiLocalizer>();
        builder.Services.AddSingleton<IUserDialogService, WpfUserDialogService>();
        builder.Services.AddSingleton<IEditorInsertDialogs, WpfEditorInsertDialogs>();
        builder.Services.AddSingleton<Lazy<INavigationService>>(static sp =>
            new Lazy<INavigationService>(() => sp.GetRequiredService<INavigationService>()));
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton(sp => new MainWindow(sp.GetRequiredService<ShellViewModel>()));

        _host = builder.Build();
        UiLocalizerHost.Current = _host.Services.GetRequiredService<IUiLocalizer>();
        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs exitEventArgs)
    {
        _host?.Dispose();
        base.OnExit(exitEventArgs);
    }
}
