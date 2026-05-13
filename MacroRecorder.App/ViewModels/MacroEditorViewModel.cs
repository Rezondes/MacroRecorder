using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Editor;
using MacroRecorder.App.Localization;
using MacroRecorder.App.Services;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Persistence;

namespace MacroRecorder.App.ViewModels;

public partial class MacroEditorViewModel : ObservableObject
{
    private const int MaxUndo = 50;
    private readonly MacroWorkspaceService _workspace;
    private readonly IUserDialogService _dialogs;
    private readonly IEditorInsertDialogs _insertDialogs;
    private readonly IPlaybackService _playback;
    private readonly RecordingCoordinator _recording;
    private readonly IUiLocalizer _loc;
    private readonly InAppInfoMessageChannel _inAppInfo;
    private readonly Dispatcher _uiDispatcher;
    private readonly List<RecordedInputEvent> _flatEvents = new();
    private readonly Stack<List<RecordedInputEvent>> _undo = new();
    private readonly Stack<List<RecordedInputEvent>> _redo = new();
    private Macro? _macro;
    private Window? _ownerWindow;
    private bool _isDirty;
    private bool _persistedOnDisk = true;
    private List<RecordedInputEvent>? _recordingSnapshot;
    private readonly Action? _onMacroSaved;

    public event Action? RequestTimelineScrollToEnd;

    public MacroEditorViewModel(
        MacroWorkspaceService workspace,
        IUserDialogService dialogs,
        IEditorInsertDialogs insertDialogs,
        IPlaybackService playback,
        RecordingCoordinator recording,
        IUiLocalizer uiLocalizer,
        InAppInfoMessageChannel inAppInfo,
        MacroId macroId,
        bool loadFromDisk,
        Macro? inMemoryMacro,
        Action? onMacroSaved = null)
    {
        _workspace = workspace;
        _dialogs = dialogs;
        _insertDialogs = insertDialogs;
        _playback = playback;
        _recording = recording;
        _loc = uiLocalizer;
        _inAppInfo = inAppInfo;
        _onMacroSaved = onMacroSaved;
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        MacroId = macroId;
        WindowTitle = _loc.GetString("Editor_DefaultWindowTitle");
        RecordingButtonCaption = _loc.GetString("Editor_RecordingStart");
        if (!loadFromDisk)
        {
            if (inMemoryMacro is null)
                throw new ArgumentNullException(nameof(inMemoryMacro));
            ApplyInMemoryMacro(inMemoryMacro);
        }
        else
            _ = LoadAsync();

        _loc.UiCultureChanged += (_, _) => OnUiCultureChanged();
    }

    public MacroId MacroId { get; }

    public string? MacroNameForFileExport => _macro?.Name;

    public string GetSerializedMacroJson()
    {
        if (_macro is null)
            return string.Empty;

        var ordered = _flatEvents.OrderBy(recordedEvent => recordedEvent.Sequence).Select(CloneEvent).ToList();
        if (_isDirty)
            TimelineNormalizer.NormalizeInPlace(ordered);

        var snapshot = new Macro(
            _macro.Id,
            _macro.Name,
            _macro.Metadata,
            ordered,
            _macro.WasModifiedAfterRecording,
            _macro.DocumentVersion,
            _macro.CreatedAtUtc,
            _macro.LastModifiedAtUtc);
        return MacroJsonFileFormat.Serialize(snapshot);
    }

    private void OnUiCultureChanged()
    {
        UpdateRecordingCaption();
        if (_macro is not null)
            WindowTitle = _loc.GetString("Editor_WindowTitleFormat", _macro.Name);
        else
            WindowTitle = _loc.GetString("Editor_DefaultWindowTitle");
        RebuildRows();
    }

    public ObservableCollection<EditorTimelineRow> Rows { get; } = new();

    [ObservableProperty]
    private EditorTimelineRow? selectedRow;

    [ObservableProperty]
    private string windowTitle = "";

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    private bool isRecording;

    [ObservableProperty]
    private bool editorHasMacro;

    [ObservableProperty]
    private bool canUseCommandsWhileNotRecording = true;

    [ObservableProperty]
    private string recordingButtonCaption = "";

    [ObservableProperty]
    private bool recordMouseMoves = true;

    [ObservableProperty]
    private bool useFocusBoundMousePositions;

    public void AttachOwner(Window owner) => _ownerWindow = owner;

    internal bool TryAbortRecordingForClose()
    {
        if (!IsRecording)
            return true;
        if (!_dialogs.Confirm(_loc.GetString("Editor_ConfirmCloseWhileRecording")))
            return false;
        AbortRecordingDiscardSession();
        return true;
    }

    public void AbortRecordingDiscardSession()
    {
        if (!_recording.IsRecording)
        {
            IsRecording = false;
            RefreshCommandStates();
            return;
        }

        _recording.AbortRecording();
        if (_recordingSnapshot is not null)
        {
            _flatEvents.Clear();
            _flatEvents.AddRange(_recordingSnapshot.Select(CloneEvent));
            _recordingSnapshot = null;
        }

        IsRecording = false;
        RebuildRows();
        RefreshCommandStates();
    }

    private void NotifySaveCanExecute() => SaveCommand.NotifyCanExecuteChanged();

    private void RefreshCommandStates()
    {
        UpdateRecordingCaption();
        NotifySaveCanExecute();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        MoveRowUpCommand.NotifyCanExecuteChanged();
        MoveRowDownCommand.NotifyCanExecuteChanged();
        DuplicateSelectedCommand.NotifyCanExecuteChanged();
        EditSelectedCommand.NotifyCanExecuteChanged();
        InsertWaitAfterCommand.NotifyCanExecuteChanged();
        InsertWaitBeforeCommand.NotifyCanExecuteChanged();
        InsertMouseClickAfterCommand.NotifyCanExecuteChanged();
        InsertMouseClickBeforeCommand.NotifyCanExecuteChanged();
        InsertKeyAfterCommand.NotifyCanExecuteChanged();
        InsertKeyBeforeCommand.NotifyCanExecuteChanged();
        ToggleRecordingCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        PlayTestCommand.NotifyCanExecuteChanged();
        RenameMacroCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsRecordingChanged(bool value)
    {
        CanUseCommandsWhileNotRecording = _macro is not null && !IsRecording;
        RefreshCommandStates();
    }

    private void UpdateRecordingCaption() =>
        RecordingButtonCaption = IsRecording ? _loc.GetString("Editor_RecordingStop") : _loc.GetString("Editor_RecordingStart");

    private void ApplyInMemoryMacro(Macro macro)
    {
        _persistedOnDisk = false;
        _macro = macro;
        WindowTitle = _loc.GetString("Editor_WindowTitleFormat", _macro.Name);
        _flatEvents.Clear();
        _flatEvents.AddRange(_macro.Events.OrderBy(recordedEvent => recordedEvent.Sequence));
        _isDirty = false;
        _undo.Clear();
        _redo.Clear();
        RebuildRows();
        EditorHasMacro = true;
        CanUseCommandsWhileNotRecording = !IsRecording;
        UpdateRecordingCaption();
        NotifySaveCanExecute();
        RefreshCommandStates();
        SyncEditorOptionsFromMacroMetadata();
    }

    private async Task LoadAsync()
    {
        _macro = await _workspace.GetAsync(MacroId).ConfigureAwait(true);
        if (_macro is null)
        {
            _dialogs.ShowInfo(_loc.GetString("Editor_MacroNotFound"));
            EditorHasMacro = false;
            return;
        }

        _persistedOnDisk = true;
        WindowTitle = _loc.GetString("Editor_WindowTitleFormat", _macro.Name);
        _flatEvents.Clear();
        _flatEvents.AddRange(_macro.Events.OrderBy(recordedEvent => recordedEvent.Sequence));
        _isDirty = false;
        _undo.Clear();
        _redo.Clear();
        RebuildRows();
        EditorHasMacro = true;
        CanUseCommandsWhileNotRecording = !IsRecording;
        UpdateRecordingCaption();
        NotifySaveCanExecute();
        RefreshCommandStates();
        SyncEditorOptionsFromMacroMetadata();
    }

    private void SyncEditorOptionsFromMacroMetadata()
    {
        if (_macro is null)
            return;
        UseFocusBoundMousePositions = _macro.Metadata.UseFocusBoundMouseCoordinates;
    }

    partial void OnUseFocusBoundMousePositionsChanged(bool value)
    {
        if (_macro is null || IsRecording)
            return;
        var meta = _macro.Metadata;
        if (meta.UseFocusBoundMouseCoordinates == value && meta.MouseAnchor is null)
            return;
        _macro.SetMetadata(meta with
        {
            UseFocusBoundMouseCoordinates = value,
            MouseAnchor = null
        });
        _isDirty = true;
        NotifySaveCanExecute();
    }

    private void RebuildRows()
    {
        Rows.Clear();
        foreach (var row in EditorTimelineGrouper.BuildRows(_flatEvents, _loc))
            Rows.Add(row);
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        var playbackTotal = PlaybackDurationEstimator.EstimateTotalPlaybackDuration(_flatEvents);
        var durationText = playbackTotal.ToString(@"hh\:mm\:ss\.fff", _loc.CurrentUiCulture);

        if (IsRecording)
        {
            StatusText =
                _loc.GetString("Editor_StatusRecordingFormat", Rows.Count, _flatEvents.Count, durationText);
            return;
        }

        if (_flatEvents.Count == 0)
        {
            StatusText = _loc.GetString("Editor_StatusNoEvents");
            return;
        }

        StatusText =
            _loc.GetString("Editor_StatusSavedFormat", Rows.Count, _flatEvents.Count, durationText);
    }

    private void PushUndo()
    {
        if (_undo.Count >= MaxUndo)
        {
            var arr = _undo.ToArray();
            _undo.Clear();
            foreach (var s in arr.Skip(1))
                _undo.Push(s);
        }

        _undo.Push(CloneFlat());
        _redo.Clear();
    }

    private List<RecordedInputEvent> CloneFlat() => _flatEvents.Select(CloneEvent).ToList();

    private static RecordedInputEvent CloneEvent(RecordedInputEvent recordedEvent) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { },
            KeyUpRecordedEvent keyUp => keyUp with { },
            MouseMoveRecordedEvent mouseMove => mouseMove with { },
            MouseButtonDownRecordedEvent mouseButtonDown => mouseButtonDown with { },
            MouseButtonUpRecordedEvent mouseButtonUp => mouseButtonUp with { },
            MouseWheelRecordedEvent mouseWheel => mouseWheel with { },
            FocusChangedRecordedEvent focusChanged => focusChanged with { },
            SyntheticWaitRecordedEvent syntheticWait => syntheticWait with { },
            _ => throw new InvalidOperationException(recordedEvent.GetType().Name)
        };

    private void RestoreFromSnapshot(List<RecordedInputEvent> snapshot)
    {
        _flatEvents.Clear();
        _flatEvents.AddRange(snapshot.Select(CloneEvent));
        RebuildRows();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undo.Count == 0)
            return;
        _redo.Push(CloneFlat());
        var previousSnapshot = _undo.Pop();
        RestoreFromSnapshot(previousSnapshot);
        _isDirty = true;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        NotifySaveCanExecute();
    }

    private bool CanUndo() => !IsRecording && _undo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redo.Count == 0)
            return;
        _undo.Push(CloneFlat());
        var nextSnapshot = _redo.Pop();
        RestoreFromSnapshot(nextSnapshot);
        _isDirty = true;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        NotifySaveCanExecute();
    }

    private bool CanRedo() => !IsRecording && _redo.Count > 0;

    private void Mutate(Action action)
    {
        if (IsRecording)
            return;
        PushUndo();
        action();
        TimelineNormalizer.NormalizeInPlace(_flatEvents);
        RebuildRows();
        SelectedRow = null;
        _isDirty = true;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        NotifySaveCanExecute();
    }

    private bool TryGetFlatSpan(EditorTimelineRow? row, out int start, out int length)
    {
        start = 0;
        length = 0;
        if (row is EditorSingleEventRow singleRow)
        {
            var flatEventIndex = _flatEvents.FindIndex(flatEvent => ReferenceEquals(flatEvent, singleRow.Event));
            if (flatEventIndex < 0)
                return false;
            start = flatEventIndex;
            length = 1;
            return true;
        }

        if (row is EditorMousePathRow mousePathRow)
        {
            if (mousePathRow.Moves.Count == 0)
                return false;
            var pathStartIndex = _flatEvents.FindIndex(flatEvent => ReferenceEquals(flatEvent, mousePathRow.Moves[0]));
            if (pathStartIndex < 0)
                return false;
            start = pathStartIndex;
            length = mousePathRow.Moves.Count;
            return true;
        }

        return false;
    }

    private int GetInsertIndex(bool afterSelection)
    {
        if (SelectedRow is null || !TryGetFlatSpan(SelectedRow, out var selectedFlatStart, out var selectedFlatLength))
            return afterSelection ? _flatEvents.Count : 0;
        return afterSelection ? selectedFlatStart + selectedFlatLength : selectedFlatStart;
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void DeleteSelected()
    {
        if (SelectedRow is null)
            return;

        Mutate(() =>
        {
            if (!TryGetFlatSpan(SelectedRow, out var flatStart, out var flatLength))
                return;

            var flatEnd = flatStart + flatLength - 1;
            var gap = TimelinePlaybackGapCollapse.ComputeGapToSubtractBeforeRemovingRange(_flatEvents, flatStart, flatEnd);

            switch (SelectedRow)
            {
                case EditorMousePathRow path:
                    foreach (var m in path.Moves)
                        _flatEvents.RemoveAll(e => ReferenceEquals(e, m));
                    break;
                case EditorSingleEventRow single:
                    _flatEvents.RemoveAll(e => ReferenceEquals(e, single.Event));
                    break;
            }

            if (gap > TimeSpan.Zero && flatStart < _flatEvents.Count)
                TimelinePlaybackGapCollapse.ShiftElapsedEarlierFromIndex(_flatEvents, flatStart, gap);
        });
    }

    private bool CanDelete() => !IsRecording && SelectedRow is not null;

    partial void OnSelectedRowChanged(EditorTimelineRow? value)
    {
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        MoveRowUpCommand.NotifyCanExecuteChanged();
        MoveRowDownCommand.NotifyCanExecuteChanged();
        DuplicateSelectedCommand.NotifyCanExecuteChanged();
        EditSelectedCommand.NotifyCanExecuteChanged();
    }

    private bool TryGetSelectedRowIndex(out int index)
    {
        index = SelectedRow is null ? -1 : Rows.IndexOf(SelectedRow);
        return index >= 0;
    }

    private void SelectRowByIndex(int index)
    {
        if (index >= 0 && index < Rows.Count)
            SelectedRow = Rows[index];
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveRowUp()
    {
        if (!TryGetSelectedRowIndex(out var selectedRowIndex) || selectedRowIndex == 0)
            return;
        var selectedAfterMove = selectedRowIndex - 1;
        Mutate(() =>
        {
            if (!TryGetFlatSpan(Rows[selectedRowIndex - 1], out var aboveRowFlatStart, out var aboveRowFlatLength))
                return;
            if (!TryGetFlatSpan(Rows[selectedRowIndex], out var selectedRowFlatStart, out var selectedRowFlatLength))
                return;
            if (selectedRowFlatStart < aboveRowFlatStart)
                return;
            var movedBlock = _flatEvents.Skip(selectedRowFlatStart).Take(selectedRowFlatLength).Select(CloneEvent).ToList();
            _flatEvents.RemoveRange(selectedRowFlatStart, selectedRowFlatLength);
            _flatEvents.InsertRange(aboveRowFlatStart, movedBlock);
        });
        SelectRowByIndex(selectedAfterMove);
    }

    private bool CanMoveUp() => !IsRecording && TryGetSelectedRowIndex(out var selectedRowIndex) && selectedRowIndex > 0;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveRowDown()
    {
        if (!TryGetSelectedRowIndex(out var selectedRowIndex) || selectedRowIndex >= Rows.Count - 1)
            return;
        var selectedAfterMove = selectedRowIndex + 1;
        Mutate(() =>
        {
            if (!TryGetFlatSpan(Rows[selectedRowIndex], out var selectedRowFlatStart, out var selectedRowFlatLength))
                return;
            if (!TryGetFlatSpan(Rows[selectedRowIndex + 1], out var belowRowFlatStart, out var belowRowFlatLength))
                return;
            if (belowRowFlatStart < selectedRowFlatStart)
                return;
            var movedBlock = _flatEvents.Skip(selectedRowFlatStart).Take(selectedRowFlatLength).Select(CloneEvent).ToList();
            _flatEvents.RemoveRange(selectedRowFlatStart, selectedRowFlatLength);
            _flatEvents.InsertRange(selectedRowFlatStart + belowRowFlatLength, movedBlock);
        });
        SelectRowByIndex(selectedAfterMove);
    }

    private bool CanMoveDown() => !IsRecording && TryGetSelectedRowIndex(out var selectedRowIndex) && selectedRowIndex < Rows.Count - 1;

    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private void DuplicateSelected()
    {
        if (SelectedRow is null || !TryGetFlatSpan(SelectedRow, out var duplicateFlatStart, out var duplicateFlatLength))
            return;
        Mutate(() =>
        {
            var clones = _flatEvents.Skip(duplicateFlatStart).Take(duplicateFlatLength).Select(CloneEvent).ToList();
            _flatEvents.InsertRange(duplicateFlatStart + duplicateFlatLength, clones);
        });
    }

    private bool CanDuplicate() => !IsRecording && SelectedRow is not null;

    /// <summary>
    /// Moves the timeline row at <paramref name="fromIndex"/> so it sits directly before the row
    /// that currently has index <paramref name="insertBeforeRowIndex"/> (0 = before first row).
    /// Use <see cref="Rows"/>.Count to append after the last row.
    /// </summary>
    public void ReorderRowDrag(int fromIndex, int insertBeforeRowIndex)
    {
        if (IsRecording)
            return;
        if (fromIndex < 0 || fromIndex >= Rows.Count)
            return;
        if (insertBeforeRowIndex < 0 || insertBeforeRowIndex > Rows.Count)
            return;
        if (insertBeforeRowIndex == fromIndex || insertBeforeRowIndex == fromIndex + 1)
            return;

        Mutate(() =>
        {
            if (!TryGetFlatSpan(Rows[fromIndex], out var fromRowFlatStart, out var fromRowFlatLength))
                return;

            int targetRowFlatStart;
            if (insertBeforeRowIndex >= Rows.Count)
                targetRowFlatStart = _flatEvents.Count;
            else if (!TryGetFlatSpan(Rows[insertBeforeRowIndex], out targetRowFlatStart, out _))
                return;

            var movedBlock = _flatEvents.Skip(fromRowFlatStart).Take(fromRowFlatLength).Select(CloneEvent).ToList();
            _flatEvents.RemoveRange(fromRowFlatStart, fromRowFlatLength);

            int flatInsertIndex;
            if (insertBeforeRowIndex >= Rows.Count)
                flatInsertIndex = _flatEvents.Count;
            else if (fromIndex < insertBeforeRowIndex)
                flatInsertIndex = targetRowFlatStart - fromRowFlatLength;
            else
                flatInsertIndex = targetRowFlatStart;

            _flatEvents.InsertRange(flatInsertIndex, movedBlock);
        });
    }

    [RelayCommand(CanExecute = nameof(CanInsertWhileNotRecording))]
    private void InsertWaitAfter() => InsertWait(true);

    [RelayCommand(CanExecute = nameof(CanInsertWhileNotRecording))]
    private void InsertWaitBefore() => InsertWait(false);

    private void InsertWait(bool after)
    {
        if (IsRecording)
            return;
        var owner = _ownerWindow ?? global::System.Windows.Application.Current?.MainWindow;
        if (owner is null)
            return;

        var draft = new SyntheticWaitRecordedEvent
        {
            DelayBefore = TimeSpan.Zero,
            Sequence = 0,
            AdditionalDelay = TimeSpan.FromMilliseconds(500)
        };

        if (_insertDialogs.ShowEditSingleEventDialog(owner, draft, out var edited) != true)
            return;
        if (edited is not SyntheticWaitRecordedEvent waitEvent)
            return;

        var insertAt = GetInsertIndex(after);
        Mutate(() =>
        {
            var totalDuration = waitEvent.DelayBefore + waitEvent.AdditionalDelay;

            if (insertAt < _flatEvents.Count)
            {
                TimeSpan gapBefore;
                if (insertAt == 0)
                {
                    gapBefore = EventPlaybackSchedule.GetWaitUntilTarget(_flatEvents, 0);
                    if (gapBefore < TimeSpan.Zero)
                        gapBefore = TimeSpan.Zero;
                }
                else
                {
                    var prevEnd = TimelinePlaybackGapCollapse.PlaybackEndAfterEvent(_flatEvents, insertAt - 1);
                    var nextStart = EventPlaybackSchedule.GetWaitUntilTarget(_flatEvents, insertAt);
                    gapBefore = nextStart - prevEnd;
                    if (gapBefore < TimeSpan.Zero)
                        gapBefore = TimeSpan.Zero;
                }

                _flatEvents.Insert(insertAt, waitEvent);
                var delta = totalDuration - gapBefore;
                if (delta != TimeSpan.Zero && insertAt + 1 < _flatEvents.Count)
                    TimelinePlaybackGapCollapse.ShiftElapsedFromIndex(_flatEvents, insertAt + 1, delta);
            }
            else
                _flatEvents.Add(waitEvent);
        });
    }

    private bool CanInsertWhileNotRecording() => !IsRecording && _macro is not null;

    [RelayCommand(CanExecute = nameof(CanInsertWhileNotRecording))]
    private void InsertMouseClickAfter() => InsertMouseClick(true);

    [RelayCommand(CanExecute = nameof(CanInsertWhileNotRecording))]
    private void InsertMouseClickBefore() => InsertMouseClick(false);

    private void InsertMouseClick(bool after)
    {
        if (IsRecording)
            return;
        var owner = _ownerWindow ?? global::System.Windows.Application.Current?.MainWindow;
        if (owner is null)
            return;

        var downDraft = new MouseButtonDownRecordedEvent
        {
            DelayBefore = TimeSpan.Zero,
            Sequence = 0,
            Button = MouseButtonKind.Left,
            ScreenX = 0,
            ScreenY = 0
        };

        if (_insertDialogs.ShowEditSingleEventDialog(owner, downDraft, out var editedDown) != true)
            return;
        if (editedDown is not MouseButtonDownRecordedEvent mouseDownOk)
            return;

        var upDraft = new MouseButtonUpRecordedEvent
        {
            DelayBefore = TimeSpan.Zero,
            Sequence = 0,
            Button = mouseDownOk.Button,
            ScreenX = mouseDownOk.ScreenX,
            ScreenY = mouseDownOk.ScreenY
        };

        if (_insertDialogs.ShowEditSingleEventDialog(owner, upDraft, out var editedUp) != true)
            return;
        if (editedUp is not MouseButtonUpRecordedEvent mouseUpOk)
            return;

        var insertAt = GetInsertIndex(after);
        Mutate(() => { _flatEvents.InsertRange(insertAt, [mouseDownOk, mouseUpOk]); });
    }

    [RelayCommand(CanExecute = nameof(CanInsertWhileNotRecording))]
    private void InsertKeyAfter() => InsertKey(true);

    [RelayCommand(CanExecute = nameof(CanInsertWhileNotRecording))]
    private void InsertKeyBefore() => InsertKey(false);

    private void InsertKey(bool after)
    {
        if (IsRecording)
            return;
        var owner = _ownerWindow ?? global::System.Windows.Application.Current?.MainWindow;
        if (owner is null)
            return;

        var defaultVk = (ushort)System.Windows.Input.KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.Space);
        var downDraft = new KeyDownRecordedEvent
        {
            DelayBefore = TimeSpan.Zero,
            Sequence = 0,
            Vk = defaultVk,
            ScanCode = MacroRecorder.Infrastructure.Input.VkScanMapper.VirtualKeyToScanCode(defaultVk),
            IsExtendedKey = false,
            IsAltDown = false,
            IsInjected = false,
            RepeatCount = 1
        };

        if (_insertDialogs.ShowEditSingleEventDialog(owner, downDraft, out var editedDown) != true)
            return;
        if (editedDown is not KeyDownRecordedEvent keyDownOk)
            return;

        var upDraft = new KeyUpRecordedEvent
        {
            DelayBefore = TimeSpan.Zero,
            Sequence = 0,
            Vk = keyDownOk.Vk,
            ScanCode = keyDownOk.ScanCode,
            IsExtendedKey = keyDownOk.IsExtendedKey,
            IsAltDown = keyDownOk.IsAltDown,
            IsInjected = keyDownOk.IsInjected
        };

        if (_insertDialogs.ShowEditSingleEventDialog(owner, upDraft, out var editedUp) != true)
            return;
        if (editedUp is not KeyUpRecordedEvent keyUpOk)
            return;

        var insertAt = GetInsertIndex(after);
        Mutate(() => { _flatEvents.InsertRange(insertAt, [keyDownOk, keyUpOk]); });
    }

    [RelayCommand(CanExecute = nameof(CanToggleRecording))]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
            await StopRecordingAsync().ConfigureAwait(true);
        else
            StartRecording();
        await Task.Yield();
    }

    private bool CanToggleRecording() => _macro is not null;

    private void StartRecording()
    {
        if (_macro is null || IsRecording || _recording.IsRecording)
            return;
        try
        {
            _recordingSnapshot = CloneFlat();
            _flatEvents.Clear();
            _flatEvents.AddRange(_recordingSnapshot.Select(CloneEvent));
            RebuildRows();
            _undo.Clear();
            _redo.Clear();
            IsRecording = true;
            RefreshCommandStates();
            var minMouseMovePixels = AppSettingsStore.Load().RecordingMouseMoveMinPixels;
            _recording.StartRecording(
                OnLiveRecordedEvent,
                RecordMouseMoves,
                minMouseMovePixels,
                UseFocusBoundMousePositions);
        }
        catch (Exception exception)
        {
            if (_recording.IsRecording)
                _recording.AbortRecording();
            if (_recordingSnapshot is not null)
            {
                _flatEvents.Clear();
                _flatEvents.AddRange(_recordingSnapshot.Select(CloneEvent));
                _recordingSnapshot = null;
            }

            IsRecording = false;
            RebuildRows();
            RefreshCommandStates();
            _dialogs.ShowInfo(_loc.GetString("Editor_RecordingStartErrorFormat", exception.Message));
        }
    }

    private void OnLiveRecordedEvent(RecordedInputEvent recordedEvent)
    {
        if (_uiDispatcher.CheckAccess())
            AppendLiveEvent(recordedEvent);
        else
            _uiDispatcher.BeginInvoke(() => AppendLiveEvent(recordedEvent));
    }

    private void AppendLiveEvent(RecordedInputEvent recordedEvent)
    {
        if (!IsRecording)
            return;
        _flatEvents.Add(CloneEvent(recordedEvent));
        RebuildRows();
        RequestTimelineScrollToEnd?.Invoke();
    }

    private async Task StopRecordingAsync()
    {
        if (!_recording.IsRecording)
        {
            IsRecording = false;
            RefreshCommandStates();
            return;
        }

        RecordingEngineResult result;
        try
        {
            result = _recording.StopRecording();
        }
        catch (Exception)
        {
            _dialogs.ShowInfo(_loc.GetString("Editor_RecordingStopError"));
            IsRecording = false;
            RefreshCommandStates();
            return;
        }

        IsRecording = false;

        var meta = RecordingMetadata.ForNewSession(result.Environment) with
        {
            UseFocusBoundMouseCoordinates = result.UseFocusBoundMouseCoordinates,
            MouseAnchor = null
        };
        var merged = new List<RecordedInputEvent>();
        if (_recordingSnapshot is not null)
            merged.AddRange(_recordingSnapshot.Select(CloneEvent));
        merged.AddRange(result.Events);
        TimelineNormalizer.NormalizeInPlace(merged);

        _flatEvents.Clear();
        _flatEvents.AddRange(merged);
        _macro!.ApplyRecordingMerge(merged, meta);
        SyncEditorOptionsFromMacroMetadata();
        _recordingSnapshot = null;
        _isDirty = true;
        RebuildRows();
        RefreshCommandStates();
        await Task.Yield();
    }

    [RelayCommand(CanExecute = nameof(CanRenameWhileNotRecording))]
    private void RenameMacro()
    {
        if (IsRecording || _macro is null)
            return;
        var owner = _ownerWindow ?? global::System.Windows.Application.Current?.MainWindow;
        if (owner is null)
            return;
        var showDialogResult = _insertDialogs.ShowRenameMacroDialog(owner, _macro.Name, out var newMacroName);
        if (showDialogResult != true)
            return;
        _macro.Rename(newMacroName);
        WindowTitle = _loc.GetString("Editor_WindowTitleFormat", _macro.Name);
        _isDirty = true;
        NotifySaveCanExecute();
    }

    private bool CanRenameWhileNotRecording() => !IsRecording && _macro is not null;

    [RelayCommand(CanExecute = nameof(CanPlayTestWhileNotRecording))]
    private async Task PlayTestAsync()
    {
        if (IsRecording || _macro is null || _flatEvents.Count == 0)
            return;
        try
        {
            var snap = _flatEvents.Select(CloneEvent).ToList();
            TimelineNormalizer.NormalizeInPlace(snap);
            var test = new Macro(
                _macro.Id,
                _macro.Name,
                _macro.Metadata,
                snap,
                _macro.WasModifiedAfterRecording,
                _macro.DocumentVersion,
                _macro.CreatedAtUtc,
                _macro.LastModifiedAtUtc);
            var graceMs = AppSettingsStore.Load().PlaybackUserInterruptGraceMs;
            await _playback.PlayAsync(test, cancellationToken: default, userInputInterruptGraceMilliseconds: graceMs).ConfigureAwait(true);
        }
        catch (PlaybackAbortedByUserRequestException)
        {
            // User cancelled from overlay during start delay; no toast or cursor restore.
        }
        catch (PlaybackInterruptedByUserException)
        {
            _inAppInfo.RequestInfo(
                _loc.GetString("Editor_PlayTestInterrupted"),
                _loc.GetString("Main_PlaybackInterruptedModalTitle"));
        }
        catch (PlaybackFocusTargetException focusTargetException)
        {
            _dialogs.ShowInfo(PlaybackFocusTargetUi.FormatMessage(_loc, focusTargetException));
        }
        catch (InvalidOperationException)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorPlaybackFailed"));
        }
        catch (Exception exception)
        {
            _dialogs.ShowInfo(_loc.GetString("Editor_PlayTest_ErrorFormat", exception.Message));
        }
    }

    private bool CanPlayTestWhileNotRecording() => !IsRecording && _macro is not null && _flatEvents.Count > 0;

    [RelayCommand(CanExecute = nameof(CanEditOrInform))]
    private void EditSelected()
    {
        if (SelectedRow is EditorMousePathRow)
        {
            _dialogs.ShowInfo(_loc.GetString("Editor_EditMousePathNotSupported"));
            return;
        }

        if (SelectedRow is not EditorSingleEventRow single)
            return;

        var owner = _ownerWindow ?? global::System.Windows.Application.Current?.MainWindow;
        if (owner is null)
            return;
        var showDialogResult = _insertDialogs.ShowEditSingleEventDialog(owner, single.Event, out var updatedEvent);
        if (showDialogResult != true)
            return;
        Mutate(() =>
        {
            var eventIndexInFlatList = _flatEvents.FindIndex(flatEvent => ReferenceEquals(flatEvent, single.Event));
            if (eventIndexInFlatList >= 0)
                _flatEvents[eventIndexInFlatList] = updatedEvent;
        });
    }

    private bool CanEditOrInform() => !IsRecording && SelectedRow is not null;

    private bool HasUnsavedEditorState()
    {
        if (_macro is null)
            return false;
        if (!_persistedOnDisk)
            return _isDirty || _flatEvents.Count > 0;
        return _isDirty;
    }

    internal async Task<EditorLeaveResult> TryLeaveEditorAsync()
    {
        if (!TryAbortRecordingForClose())
            return EditorLeaveResult.Cancel;

        if (!HasUnsavedEditorState())
            return EditorLeaveResult.Proceed;

        var macroName = _macro?.Name ?? "";
        var choice = _dialogs.PromptUnsavedChanges(
            _loc.GetString("Editor_UnsavedChangesMessage", macroName),
            _loc.GetString("Editor_UnsavedChangesTitle"));

        switch (choice)
        {
            case UnsavedChangesPromptResult.Cancel:
                return EditorLeaveResult.Cancel;
            case UnsavedChangesPromptResult.Discard:
                return EditorLeaveResult.Proceed;
            case UnsavedChangesPromptResult.Save:
                if (!await TryPersistCurrentMacroAsync(showSavedToast: true).ConfigureAwait(true))
                    return EditorLeaveResult.Cancel;
                return EditorLeaveResult.Proceed;
            default:
                return EditorLeaveResult.Cancel;
        }
    }

    private bool CanSave() => _macro is not null && !IsRecording && (_isDirty || !_persistedOnDisk);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        _ = await TryPersistCurrentMacroAsync(showSavedToast: true).ConfigureAwait(true);
    }

    private async Task<bool> TryPersistCurrentMacroAsync(bool showSavedToast)
    {
        if (IsRecording || _macro is null)
            return false;

        if (!_persistedOnDisk)
        {
            var name = _dialogs.PromptText(
                _loc.GetString("Editor_FirstSaveTitle"),
                _loc.GetString("Editor_FirstSaveMessage"),
                _macro.Name);
            if (string.IsNullOrWhiteSpace(name))
                return false;
            _macro.AssignNameOnly(name.Trim());
            WindowTitle = _loc.GetString("Editor_WindowTitleFormat", _macro.Name);
        }

        if (_isDirty)
            TimelineNormalizer.NormalizeInPlace(_flatEvents);
        var ordered = _flatEvents.OrderBy(recordedEvent => recordedEvent.Sequence).ToList();
        var versionBump = _isDirty || !_persistedOnDisk;
        _macro!.ApplyPersistedEditorState(ordered, markRecordedDirty: true, bumpDocumentVersion: versionBump);
        await _workspace.SaveAsync(_macro).ConfigureAwait(true);
        _flatEvents.Clear();
        _flatEvents.AddRange(ordered);
        _isDirty = false;
        _persistedOnDisk = true;
        _undo.Clear();
        _redo.Clear();
        RebuildRows();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        NotifySaveCanExecute();
        if (showSavedToast)
            _dialogs.ShowInfo(_loc.GetString("Editor_Saved"));
        _onMacroSaved?.Invoke();
        return true;
    }
}
