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
        builder.Services.AddSingleton<AppearanceService>();
        builder.Services.AddSingleton<InAppInfoMessageChannel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<IUiLocalizer, ResxUiLocalizer>();
        builder.Services.AddSingleton<Lazy<INavigationService>>(static sp =>
            new Lazy<INavigationService>(() => sp.GetRequiredService<INavigationService>()));
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton(sp =>
            new Lazy<IUnsavedChangesModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton(sp =>
            new Lazy<IConfirmModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton(sp =>
            new Lazy<IEditEventModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton(sp =>
            new Lazy<IEditorInsertModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton(sp =>
            new Lazy<IPromptTextModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton<IUserDialogService>(sp =>
            new WpfUserDialogService(
                sp.GetRequiredService<IUiLocalizer>(),
                sp.GetRequiredService<Lazy<IUnsavedChangesModalHost>>(),
                sp.GetRequiredService<Lazy<IConfirmModalHost>>(),
                sp.GetRequiredService<Lazy<IPromptTextModalHost>>(),
                sp.GetRequiredService<InAppInfoMessageChannel>()));
        builder.Services.AddSingleton<IEditorInsertDialogs>(sp =>
            new WpfEditorInsertDialogs(
                sp.GetRequiredService<Lazy<IEditorInsertModalHost>>(),
                sp.GetRequiredService<Lazy<IEditEventModalHost>>()));
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton(sp => new MainWindow(
            sp.GetRequiredService<ShellViewModel>(),
            sp.GetRequiredService<AppearanceService>()));

        _host = builder.Build();
        var appearance = _host.Services.GetRequiredService<AppearanceService>();
        appearance.Initialize(this);
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
