using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.App.Localization;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace MacroRecorder.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MacroWorkspaceService _workspace;
    private readonly IPlaybackService _playback;
    private readonly IUserDialogService _dialogs;
    private readonly InAppInfoMessageChannel _inAppInfo;
    private readonly Lazy<INavigationService> _navigation;
    private readonly Lazy<IExportMacroJsonModalHost> _exportModalHost;
    private readonly Lazy<IImportMacroJsonModalHost> _importModalHost;
    private readonly Lazy<IPromptPlaybackChordModalHost> _promptChordHost;
    private readonly MacroPlaybackHotkeyRegistrar _playbackHotkeyRegistrar;
    private readonly IUiLocalizer _loc;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(
        MacroWorkspaceService workspace,
        IPlaybackService playback,
        IUserDialogService dialogs,
        InAppInfoMessageChannel inAppInfo,
        Lazy<INavigationService> navigation,
        Lazy<IExportMacroJsonModalHost> exportModalHost,
        Lazy<IImportMacroJsonModalHost> importModalHost,
        Lazy<IPromptPlaybackChordModalHost> promptChordHost,
        MacroPlaybackHotkeyRegistrar playbackHotkeyRegistrar,
        IUiLocalizer loc,
        ILogger<MainViewModel> logger)
    {
        _workspace = workspace;
        _playback = playback;
        _dialogs = dialogs;
        _inAppInfo = inAppInfo;
        _navigation = navigation;
        _exportModalHost = exportModalHost;
        _importModalHost = importModalHost;
        _promptChordHost = promptChordHost;
        _playbackHotkeyRegistrar = playbackHotkeyRegistrar;
        _loc = loc;
        _logger = logger;
        _loc.UiCultureChanged += (_, _) => OnUiCultureChanged();
    }

    private void OnUiCultureChanged() => _ = RefreshAsync(suppressHotkeyRegistrationFailureDialog: true);

    public ObservableCollection<MacroSummary> Macros { get; } = new();

    /// <summary>Reloads overview rows from disk and reapplies global playback hotkeys.</summary>
    /// <returns><c>true</c> if all hotkeys were registered with Windows; <c>false</c> if registration failed (all hotkeys are unregistered).</returns>
    public async Task<bool> RefreshAsync(bool suppressHotkeyRegistrationFailureDialog = false)
    {
        var list = await _workspace.ListAsync().ConfigureAwait(true);
        Macros.Clear();
        var map = new Dictionary<MacroId, PlaybackKeyChord>();
        foreach (var s in list)
        {
            var display = s.PlaybackHotkey is { } c ? PlaybackHotkeyDisplayFormatter.Format(c, _loc) : null;
            Macros.Add(s with { PlaybackHotkeyDisplay = display });
            if (s.PlaybackHotkey is { } h)
                map[s.Id] = h;
        }

        var applied = _playbackHotkeyRegistrar.TryApplyAssignments(map, out var win32Error);
        if (!applied && !suppressHotkeyRegistrationFailureDialog)
            _dialogs.ShowInfo(_loc.GetString("Hotkey_Error_RegisterFailed", win32Error.ToString()));

        return applied;
    }

    /// <summary>Reorders the overview list and persists order for the next <see cref="RefreshAsync"/>.</summary>
    public async Task ApplyMacroReorderAsync(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
            return;
        if (oldIndex < 0 || oldIndex >= Macros.Count || newIndex < 0 || newIndex >= Macros.Count)
            return;

        Macros.Move(oldIndex, newIndex);
        try
        {
            await _workspace.SaveMacroDisplayOrderAsync(Macros.Select(static m => m.Id).ToList()).ConfigureAwait(true);
        }
        catch
        {
            await RefreshAsync(suppressHotkeyRegistrationFailureDialog: true).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task PlayAsync(MacroSummary? item)
    {
        if (item is null)
            return;
        var macro = await _workspace.GetAsync(item.Id).ConfigureAwait(true);
        if (macro is null || macro.Events.Count == 0)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorNoMacro"));
            return;
        }

        await RunPlaybackCoreAsync(macro).ConfigureAwait(true);
    }

    public async Task PlayMacroByIdAsync(MacroId macroId)
    {
        var macro = await _workspace.GetAsync(macroId).ConfigureAwait(true);
        if (macro is null || macro.Events.Count == 0)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorNoMacro"));
            return;
        }

        await RunPlaybackCoreAsync(macro).ConfigureAwait(true);
    }

    private async Task RunPlaybackCoreAsync(Macro macro)
    {
        try
        {
            var prefs = AppSettingsStore.Load();
            await _playback.PlayAsync(
                macro,
                cancellationToken: default,
                userInputInterruptGraceMilliseconds: prefs.PlaybackUserInterruptGraceMs,
                playbackFocusBringWindowToForeground: prefs.PlaybackFocusBringWindowToForeground,
                playbackFocusRestoreIfMinimized: prefs.PlaybackFocusRestoreIfMinimized).ConfigureAwait(true);
        }
        catch (PlaybackAbortedByUserRequestException)
        {
            // User cancelled from overlay during start delay; no toast or cursor restore.
        }
        catch (PlaybackInterruptedByUserException)
        {
            _inAppInfo.RequestInfo(
                _loc.GetString("Main_Play_InterruptedByUser"),
                _loc.GetString("Main_PlaybackInterruptedModalTitle"));
        }
        catch (PlaybackFocusTargetException focusTargetException)
        {
            _dialogs.ShowInfo(PlaybackFocusTargetUi.FormatMessage(_loc, focusTargetException));
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (InvalidOperationException)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorPlaybackFailed"));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Overview playback failed");
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorDetail", exception.Message));
        }
    }

    [RelayCommand]
    private void Edit(MacroSummary? item)
    {
        if (item is null)
            return;
        _navigation.Value.OpenEditor(item.Id, OnMacroListShouldRefresh);
    }

    [RelayCommand]
    private async Task DeleteAsync(MacroSummary? item)
    {
        if (item is null)
            return;
        if (!_dialogs.Confirm(_loc.GetString("Main_DeleteConfirm", item.Name)))
            return;
        await _workspace.DeleteAsync(item.Id).ConfigureAwait(true);
        await RefreshAsync(suppressHotkeyRegistrationFailureDialog: true).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExportAsync(MacroSummary? item)
    {
        if (item is null)
            return;
        var macro = await _workspace.GetAsync(item.Id).ConfigureAwait(true);
        if (macro is null)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Export_ErrorNotFound"));
            return;
        }

        var json = MacroJsonFileFormat.Serialize(macro);
        if (string.IsNullOrWhiteSpace(json))
            return;
        var macroNameForFileDialog = string.IsNullOrWhiteSpace(item.Name) ? "macro" : item.Name.Trim();
        _exportModalHost.Value.ShowExportJsonModal(macroNameForFileDialog, json);
    }

    [RelayCommand]
    private void NewRecording()
    {
        var macro = Macro.CreateEmpty(_loc.GetString("Main_NewMacroDefaultName"));
        _navigation.Value.OpenNewMacroEditor(macro, OnMacroListShouldRefresh);
    }

    [RelayCommand]
    private void ImportMacro()
    {
        _importModalHost.Value.ShowImportMacroModal(ImportMacroFromJsonAsync);
    }

    private async Task<bool> ImportMacroFromJsonAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Import_ErrorEmpty"));
            return false;
        }

        var trimmed = json.Trim();
        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var parsed = MacroJsonFileFormat.ParseMacro(document.RootElement);
            if (parsed is null)
            {
                _dialogs.ShowInfo(_loc.GetString("Main_Import_ErrorInvalid"));
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            var copy = new Macro(
                MacroId.New(),
                parsed.Name,
                parsed.Metadata,
                parsed.Events,
                parsed.WasModifiedAfterRecording,
                null,
                now,
                now);

            await _workspace.SaveAsync(copy).ConfigureAwait(true);
            await RefreshAsync(suppressHotkeyRegistrationFailureDialog: true).ConfigureAwait(true);
            _dialogs.ShowInfo(_loc.GetString("Main_Import_Success", copy.Name));
            return true;
        }
        catch (ArgumentException)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Import_ErrorInvalid"));
            return false;
        }
        catch (JsonException)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Import_ErrorInvalid"));
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Macro import failed");
            _dialogs.ShowInfo(_loc.GetString("Main_Import_ErrorLoad", exception.Message));
            return false;
        }
    }

    [RelayCommand]
    private async Task AddPlaybackHotkeyAsync(MacroSummary? item) =>
        await PromptAndSavePlaybackHotkeyAsync(item).ConfigureAwait(true);

    [RelayCommand]
    private async Task ChangePlaybackHotkeyAsync(MacroSummary? item) =>
        await PromptAndSavePlaybackHotkeyAsync(item).ConfigureAwait(true);

    [RelayCommand]
    private async Task RemovePlaybackHotkeyAsync(MacroSummary? item)
    {
        if (item is null)
            return;
        try
        {
            await _workspace.SetPlaybackHotkeyAsync(item.Id, null).ConfigureAwait(true);
            await RefreshAsync(suppressHotkeyRegistrationFailureDialog: true).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to remove playback hotkey");
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorDetail", exception.Message));
        }
    }

    private async Task PromptAndSavePlaybackHotkeyAsync(MacroSummary? item)
    {
        if (item is null)
            return;

        var list = (await _workspace.ListAsync().ConfigureAwait(true)).ToList();
        var blocked = list
            .Where(m => m.Id != item.Id && m.PlaybackHotkey is not null)
            .Select(m => m.PlaybackHotkey!.Value)
            .ToList();

        var chord = _promptChordHost.Value.PromptPlaybackChord(
            _loc.GetString("Hotkey_Capture_Title"),
            _loc.GetString("Hotkey_Capture_Message"),
            blocked);
        if (chord is null)
            return;

        if (PlaybackHotkeyRiskPolicy.IsRisky(chord.Value) &&
            !_dialogs.Confirm(_loc.GetString("Hotkey_Warn_RiskyConfirm")))
            return;

        var previous = item.PlaybackHotkey;
        try
        {
            await _workspace.SetPlaybackHotkeyAsync(item.Id, chord.Value).ConfigureAwait(true);
            var listAfterSave = (await _workspace.ListAsync().ConfigureAwait(true)).ToList();
            var map = new Dictionary<MacroId, PlaybackKeyChord>();
            foreach (var m in listAfterSave)
            {
                if (m.PlaybackHotkey is { } h)
                    map[m.Id] = h;
            }

            var applied = _playbackHotkeyRegistrar.TryApplyAssignments(map, out var regErr);
            if (applied)
            {
                await RefreshAsync(suppressHotkeyRegistrationFailureDialog: true).ConfigureAwait(true);
                return;
            }

            await _workspace.SetPlaybackHotkeyAsync(item.Id, previous).ConfigureAwait(true);
            await RefreshAsync(suppressHotkeyRegistrationFailureDialog: true).ConfigureAwait(true);
            _dialogs.ShowInfo(_loc.GetString("Hotkey_Error_RegisterFailed", regErr.ToString()));
        }
        catch (PlaybackHotkeyConflictException ex)
        {
            var otherName = list.FirstOrDefault(m => m.Id == ex.ConflictingMacroId)?.Name ?? ex.ConflictingMacroId.Value;
            _dialogs.ShowInfo(_loc.GetString("Hotkey_Error_Conflict", otherName));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save playback hotkey");
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorDetail", exception.Message));
        }
    }

    private void OnMacroListShouldRefresh() => _ = RefreshAsync(suppressHotkeyRegistrationFailureDialog: true);
}
