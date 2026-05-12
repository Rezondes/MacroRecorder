using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.ViewModels;

public partial class RecordViewModel : ObservableObject
{
    private readonly RecordingCoordinator _recording;
    private readonly MacroWorkspaceService _workspace;
    private readonly IUserDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly IUiLocalizer _loc;
    private readonly Action? _onMacroSavedFromEditor;

    public RecordViewModel(
        RecordingCoordinator recording,
        MacroWorkspaceService workspace,
        IUserDialogService dialogs,
        INavigationService navigation,
        IUiLocalizer loc,
        Action? onMacroSavedFromEditor = null)
    {
        _recording = recording;
        _workspace = workspace;
        _dialogs = dialogs;
        _navigation = navigation;
        _loc = loc;
        _onMacroSavedFromEditor = onMacroSavedFromEditor;
    }

    [ObservableProperty]
    private bool isRecording;

    [RelayCommand]
    private void Start()
    {
        if (IsRecording)
            return;
        var minMouseMovePixels = AppSettingsStore.Load().RecordingMouseMoveMinPixels;
        _recording.StartRecording(recordMouseMoves: true, mouseMoveMinPixelDelta: minMouseMovePixels);
        IsRecording = true;
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (!IsRecording)
            return;

        var name = _dialogs.PromptText(
            _loc.GetString("Record_SaveTitle"),
            _loc.GetString("Record_SaveMessage"),
            _loc.GetString("Record_DefaultMacroName"));
        if (string.IsNullOrWhiteSpace(name))
        {
            _recording.AbortRecording();
            IsRecording = false;
            return;
        }

        var macro = _recording.FinishRecording(name.Trim());
        IsRecording = false;
        await _workspace.SaveAsync(macro).ConfigureAwait(true);
        _dialogs.ShowInfo(_loc.GetString("Record_Saved"));
        if (_dialogs.Confirm(_loc.GetString("Record_OpenInEditor")))
            _navigation.OpenEditor(macro.Id, _onMacroSavedFromEditor);
    }
}
