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
        builder.Services.AddSingleton<UpdateCheckCoordinator>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<IUiLocalizer, ResxUiLocalizer>();
        builder.Services.AddSingleton(sp =>
            new MacroPlaybackHotkeyRegistrar(
                new Lazy<MainViewModel>(() => sp.GetRequiredService<MainViewModel>()),
                sp.GetRequiredService<RecordingCoordinator>()));
        builder.Services.AddSingleton<Lazy<INavigationService>>(static sp =>
            new Lazy<INavigationService>(() => sp.GetRequiredService<INavigationService>()));
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<QueueCreatorViewModel>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<IPlaybackUiFeedback>(sp => sp.GetRequiredService<ShellViewModel>());
        builder.Services.AddSingleton<IUpdatePromptModalHost>(sp => sp.GetRequiredService<ShellViewModel>());
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
        builder.Services.AddSingleton(sp =>
            new Lazy<IExportMacroJsonModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton(sp =>
            new Lazy<IImportMacroJsonModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton(sp =>
            new Lazy<IUpdatePromptModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton(sp =>
            new Lazy<IPromptPlaybackChordModalHost>(() => sp.GetRequiredService<ShellViewModel>()));
        builder.Services.AddSingleton<IUserDialogService>(sp =>
            new WpfUserDialogService(
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
            sp.GetRequiredService<AppearanceService>(),
            sp.GetRequiredService<MacroPlaybackHotkeyRegistrar>()));

        _host = builder.Build();
        var appearance = _host.Services.GetRequiredService<AppearanceService>();
        appearance.Initialize(this);
        UiLocalizerHost.Current = _host.Services.GetRequiredService<IUiLocalizer>();
        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
        _host.Services.GetRequiredService<UpdateCheckCoordinator>().RunStartupCheckIfEnabled();
    }

    protected override void OnExit(ExitEventArgs exitEventArgs)
    {
        _host?.Dispose();
        base.OnExit(exitEventArgs);
    }
}
