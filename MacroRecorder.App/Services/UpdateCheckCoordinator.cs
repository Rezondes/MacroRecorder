using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.App.Services;

namespace MacroRecorder.App.Services;

public sealed class UpdateCheckCoordinator
{
    private readonly IUpdateCheckService _updateCheckService;
    private readonly IUpdatePromptModalHost _updatePromptHost;
    private readonly IUserDialogService _dialogs;
    private readonly IUiLocalizer _loc;
    private readonly IExternalUriOpener _externalUriOpener;

    public UpdateCheckCoordinator(
        IUpdateCheckService updateCheckService,
        IUpdatePromptModalHost updatePromptHost,
        IUserDialogService dialogs,
        IUiLocalizer loc,
        IExternalUriOpener externalUriOpener)
    {
        _updateCheckService = updateCheckService;
        _updatePromptHost = updatePromptHost;
        _dialogs = dialogs;
        _loc = loc;
        _externalUriOpener = externalUriOpener;
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
        if (choice == UpdatePromptChoice.OpenRelease)
            _externalUriOpener.Open(result.ReleasePageUrl);
        else
            AppSettingsStore.SaveLastDismissedUpdateVersion(result.LatestVersion);
    }
}
