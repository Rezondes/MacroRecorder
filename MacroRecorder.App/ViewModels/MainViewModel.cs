using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Persistence;
using Microsoft.Win32;

namespace MacroRecorder.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MacroWorkspaceService _workspace;
    private readonly IPlaybackService _playback;
    private readonly IUserDialogService _dialogs;
    private readonly InAppInfoMessageChannel _inAppInfo;
    private readonly Lazy<INavigationService> _navigation;
    private readonly IUiLocalizer _loc;

    public MainViewModel(
        MacroWorkspaceService workspace,
        IPlaybackService playback,
        IUserDialogService dialogs,
        InAppInfoMessageChannel inAppInfo,
        Lazy<INavigationService> navigation,
        IUiLocalizer loc)
    {
        _workspace = workspace;
        _playback = playback;
        _dialogs = dialogs;
        _inAppInfo = inAppInfo;
        _navigation = navigation;
        _loc = loc;
        _loc.UiCultureChanged += (_, _) => OnUiCultureChanged();
    }

    private void OnUiCultureChanged()
    {
        var view = CollectionViewSource.GetDefaultView(Macros);
        view?.Refresh();
    }

    public ObservableCollection<MacroSummary> Macros { get; } = new();

    public async Task RefreshAsync()
    {
        var list = await _workspace.ListAsync().ConfigureAwait(true);
        Macros.Clear();
        foreach (var macroSummary in list)
            Macros.Add(macroSummary);
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

        try
        {
            var graceMs = AppSettingsStore.Load().PlaybackUserInterruptGraceMs;
            await _playback.PlayAsync(macro, cancellationToken: default, userInputInterruptGraceMilliseconds: graceMs).ConfigureAwait(true);
        }
        catch (PlaybackInterruptedByUserException)
        {
            _inAppInfo.RequestInfo(
                _loc.GetString("Main_Play_InterruptedByUser"),
                _loc.GetString("Main_PlaybackInterruptedModalTitle"));
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
        await RefreshAsync().ConfigureAwait(true);
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

        var dialog = new SaveFileDialog
        {
            Filter = _loc.GetString("Main_Export_FileFilter"),
            FileName = SanitizeFileNameForExport(item.Name) + ".json",
            AddExtension = true,
            DefaultExt = ".json"
        };
        var showResult = System.Windows.Application.Current?.MainWindow is { } owner
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();
        if (showResult != true || string.IsNullOrWhiteSpace(dialog.FileName))
            return;

        try
        {
            var json = MacroJsonFileFormat.Serialize(macro);
            await File.WriteAllTextAsync(dialog.FileName, json).ConfigureAwait(true);
            _dialogs.ShowInfo(_loc.GetString("Main_Export_Success", dialog.FileName));
        }
        catch (Exception exception)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Export_ErrorWrite", exception.Message));
        }
    }

    private static string SanitizeFileNameForExport(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var trimmed = new string(chars).Trim();
        return string.IsNullOrEmpty(trimmed) ? "macro" : trimmed;
    }

    [RelayCommand]
    private void NewRecording()
    {
        var macro = Macro.CreateEmpty(_loc.GetString("Main_NewMacroDefaultName"));
        _navigation.Value.OpenNewMacroEditor(macro, OnMacroListShouldRefresh);
    }

    private void OnMacroListShouldRefresh() => _ = RefreshAsync();
}
