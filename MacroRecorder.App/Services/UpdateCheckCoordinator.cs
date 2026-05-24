using System.Windows;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.App.Services;

namespace MacroRecorder.App.Services;

public sealed class UpdateCheckCoordinator
{
    private readonly IUpdateCheckService _updateCheckService;
    private readonly IUpdatePromptModalHost _updatePromptHost;
    private readonly IAppUpdateService _appUpdateService;
    private readonly IUserDialogService _dialogs;
    private readonly IUiLocalizer _loc;

    public UpdateCheckCoordinator(
        IUpdateCheckService updateCheckService,
        IUpdatePromptModalHost updatePromptHost,
        IAppUpdateService appUpdateService,
        IUserDialogService dialogs,
        IUiLocalizer loc)
    {
        _updateCheckService = updateCheckService;
        _updatePromptHost = updatePromptHost;
        _appUpdateService = appUpdateService;
        _dialogs = dialogs;
        _loc = loc;
    }

    public void RunStartupCheckIfEnabled()
    {
        _ = RunCheckAsync(isManual: false);
    }

    public Task CheckNowAsync() => RunCheckAsync(isManual: true);

    private async Task RunCheckAsync(bool isManual)
    {
        if (!isManual)
        {
            var settings = AppSettingsStore.Load();
            if (!settings.CheckForUpdatesOnStartup)
                return;
        }

        var result = await _updateCheckService.CheckForUpdateAsync().ConfigureAwait(true);
        if (result is null)
        {
            if (isManual)
                _dialogs.ShowInfo(_loc.GetString("Update_CheckFailed"));
            return;
        }

        if (!result.IsUpdateAvailable)
        {
            if (isManual)
                _dialogs.ShowInfo(_loc.GetString("Update_UpToDate"));
            return;
        }

        if (!isManual)
        {
            var settings = AppSettingsStore.Load();
            if (string.Equals(settings.LastDismissedUpdateVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
                return;
        }

        var choice = _updatePromptHost.ShowUpdateAvailable(result);
        if (choice == UpdatePromptChoice.ApplyNow)
        {
            await ApplyUpdateAsync(result).ConfigureAwait(true);
            return;
        }

        AppSettingsStore.SaveLastDismissedUpdateVersion(result.LatestVersion);
    }

    private async Task ApplyUpdateAsync(UpdateCheckResult result)
    {
        _dialogs.ShowInfo(_loc.GetString("Update_PreparingRestart"));

        var launchResult = await _appUpdateService.LaunchPortableUpdateAsync(result).ConfigureAwait(true);
        if (!launchResult.IsSuccess)
        {
            var messageKey = launchResult.FailureReason switch
            {
                AppUpdateLaunchFailureReason.UpdaterMissing => "Update_ApplyFailedUpdaterMissing",
                AppUpdateLaunchFailureReason.InstallDirectoryNotWritable => "Update_ApplyFailedNotWritable",
                AppUpdateLaunchFailureReason.PortableZipUrlMissing => "Update_ApplyFailedNoAsset",
                _ => "Update_ApplyFailed"
            };
            _dialogs.ShowInfo(_loc.GetString(messageKey));
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }
}
