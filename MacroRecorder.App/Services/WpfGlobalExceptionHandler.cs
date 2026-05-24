using System.Windows;
using System.Windows.Threading;
using MacroRecorder.Application.Ports;
using MacroRecorder.Logging;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MacroRecorder.App.Services;

public static class WpfGlobalExceptionHandler
{
    private static ILogger? _logger;
    private static IUiLocalizer? _localizer;

    public static void Register(System.Windows.Application application, ILogger logger, IUiLocalizer localizer)
    {
        _logger = logger;
        _localizer = localizer;

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
            _logger?.LogCritical(exception, "Unhandled AppDomain exception. IsTerminating={IsTerminating}", eventArgs.IsTerminating);

        FlushAndShowCrashDialog();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        _logger?.LogError(eventArgs.Exception, "Unhandled WPF dispatcher exception");
        eventArgs.Handled = true;
        ShowCrashDialogIfPossible();
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        _logger?.LogError(eventArgs.Exception, "Unobserved task exception");
        eventArgs.SetObserved();
    }

    private static void FlushAndShowCrashDialog()
    {
        Log.CloseAndFlush();
        ShowCrashDialogIfPossible();
    }

    private static void ShowCrashDialogIfPossible()
    {
        if (_localizer is null)
            return;

        try
        {
            var message = string.Format(
                _localizer.CurrentUiCulture,
                _localizer.GetString("Crash_UnexpectedError"),
                LogPaths.LogsDirectory);
            MessageBox.Show(
                message,
                _localizer.GetString("Main_WindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Best effort only.
        }
    }
}
