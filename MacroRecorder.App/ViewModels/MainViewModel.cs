using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MacroWorkspaceService _workspace;
    private readonly IPlaybackService _playback;
    private readonly IUserDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly IUiLocalizer _loc;

    public MainViewModel(
        MacroWorkspaceService workspace,
        IPlaybackService playback,
        IUserDialogService dialogs,
        INavigationService navigation,
        IUiLocalizer loc)
    {
        _workspace = workspace;
        _playback = playback;
        _dialogs = dialogs;
        _navigation = navigation;
        _loc = loc;
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
            await _playback.PlayAsync(macro).ConfigureAwait(true);
        }
        catch (PlaybackInterruptedByUserException)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Play_InterruptedByUser"));
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
        _navigation.OpenEditor(item.Id, OnMacroListShouldRefresh);
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
    private void NewRecordingAsync()
    {
        var macro = Macro.CreateEmpty(_loc.GetString("Main_NewMacroDefaultName"));
        _navigation.OpenNewMacroEditor(macro, OnMacroListShouldRefresh);
    }

    private void OnMacroListShouldRefresh() => _ = RefreshAsync();
}
