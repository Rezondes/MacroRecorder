using System.Windows;
using MacroRecorder.App.Localization;
using MacroRecorder.App.Services;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure;
using MacroRecorder.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace MacroRecorder.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs startupEventArgs)
    {
        var settings = AppSettingsStore.Load();
        var minLevel = settings.EnableVerboseLogging ? LogEventLevel.Debug : LogEventLevel.Information;
        var version = AppRuntimeInfo.Version;
        Log.Logger = LoggingBootstrap.CreateFileLogger(LogPaths.AppLogFileName, minLevel, version);

        base.OnStartup(startupEventArgs);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);
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
        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation(
            "Application starting. Version {Version}, LogsDirectory {LogsDirectory}, VerboseLogging {VerboseLogging}",
            version,
            LogPaths.LogsDirectory,
            settings.EnableVerboseLogging);
        WpfGlobalExceptionHandler.Register(
            this,
            logger,
            _host.Services.GetRequiredService<IUiLocalizer>());
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
        Log.CloseAndFlush();
        base.OnExit(exitEventArgs);
    }
}
