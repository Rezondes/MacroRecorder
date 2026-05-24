using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Views;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.App.Views.Editor;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MacroRecorder.App.ViewModels;

public partial class ShellViewModel : ObservableObject,
    IUnsavedChangesModalHost,
    IConfirmModalHost,
    IEditEventModalHost,
    IEditorInsertModalHost,
    IPromptTextModalHost,
    IPromptPlaybackChordModalHost,
    IExportMacroJsonModalHost,
    IImportMacroJsonModalHost,
    IUpdatePromptModalHost,
    IPlaybackUiFeedback
{
    private readonly MainViewModel _overview;
    private readonly IServiceProvider _services;
    private readonly IUiLocalizer _loc;
    private readonly IUserDialogService _dialogs;
    private readonly InAppInfoMessageChannel _inAppInfo;
    private readonly MacroPlaybackHotkeyRegistrar _playbackHotkeys;
    private readonly List<object> _stack = new();
    private MacroEditorViewModel? _editorTitleSource;

    public ShellViewModel(
        MainViewModel overview,
        IServiceProvider services,
        IUiLocalizer loc,
        IUserDialogService dialogs,
        InAppInfoMessageChannel inAppInfo,
        MacroPlaybackHotkeyRegistrar playbackHotkeys)
    {
        _overview = overview;
        _services = services;
        _loc = loc;
        _dialogs = dialogs;
        _inAppInfo = inAppInfo;
        _playbackHotkeys = playbackHotkeys;
        _inAppInfo.InfoRequested += OnInAppInfoRequested;
        _loc.UiCultureChanged += (_, _) => UpdateShellTitle();
        _stack.Add(overview);
        CurrentPage = overview;
        UpdateShellTitle();
        UpdateShowEditorMacroHeaderActions();
        OnPropertyChanged(nameof(ShowOverviewHeaderChrome));
    }

    public MainViewModel Overview => _overview;

    /// <summary>Caption row: New recording, queue, and settings are shown only on the macro overview page.</summary>
    public bool ShowOverviewHeaderChrome => CurrentPage is MainViewModel;

    [ObservableProperty]
    private object? currentPage;

    [ObservableProperty]
    private bool isInfoModalOpen;

    [ObservableProperty]
    private bool isUnsavedChangesModalOpen;

    [ObservableProperty]
    private string unsavedChangesModalTitle = "";

    [ObservableProperty]
    private string unsavedChangesModalMessage = "";

    [ObservableProperty]
    private string unsavedChangesModalSaveLabel = "";

    [ObservableProperty]
    private string unsavedChangesModalDiscardLabel = "";

    [ObservableProperty]
    private bool unsavedChangesModalIsAppearance;

    [ObservableProperty]
    private string infoModalTitle = "";

    [ObservableProperty]
    private string infoModalMessage = "";

    [ObservableProperty]
    private string shellTitle = "";

    [ObservableProperty]
    private bool isPinnedTopmost;

    [ObservableProperty]
    private bool isConfirmModalOpen;

    [ObservableProperty]
    private string confirmModalTitle = "";

    [ObservableProperty]
    private string confirmModalMessage = "";

    [ObservableProperty]
    private bool isContentModalOpen;

    [ObservableProperty]
    private object? contentModalContent;

    [ObservableProperty]
    private bool showEditorMacroHeaderActions;

    [RelayCommand]
    private void CloseInfoModal() => IsInfoModalOpen = false;

    private DispatcherFrame? _unsavedChangesModalFrame;
    private UnsavedChangesPromptResult? _unsavedChangesModalResult;

    private DispatcherFrame? _confirmModalFrame;
    private bool? _confirmModalResult;

    private DispatcherFrame? _contentModalFrame;
    private IContentModalEscape? _contentModalEscapeTarget;

    UnsavedChangesPromptResult IUnsavedChangesModalHost.ShowUnsavedChangesPrompt(
        string message,
        string title,
        UnsavedChangesPromptContext context)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return UnsavedChangesPromptResult.Cancel;

        if (!dispatcher.CheckAccess())
            return dispatcher.Invoke(() => ((IUnsavedChangesModalHost)this).ShowUnsavedChangesPrompt(message, title, context));

        ApplyUnsavedChangesModalContext(context);
        UnsavedChangesModalMessage = message;
        UnsavedChangesModalTitle = title;
        _unsavedChangesModalResult = null;
        IsUnsavedChangesModalOpen = true;
        _unsavedChangesModalFrame = new DispatcherFrame(true);
        System.Windows.Threading.Dispatcher.PushFrame(_unsavedChangesModalFrame);
        return _unsavedChangesModalResult ?? UnsavedChangesPromptResult.Cancel;
    }

    private void ApplyUnsavedChangesModalContext(UnsavedChangesPromptContext context)
    {
        if (context is UnsavedChangesPromptContext.Settings)
        {
            UnsavedChangesModalIsAppearance = true;
            if (context == UnsavedChangesPromptContext.Settings)
            {
                UnsavedChangesModalSaveLabel = _loc.GetString("Settings_UnsavedSave");
                UnsavedChangesModalDiscardLabel = _loc.GetString("Settings_UnsavedDiscard");
            }
            else
            {
                UnsavedChangesModalSaveLabel = _loc.GetString("Visuals_UnsavedSave");
                UnsavedChangesModalDiscardLabel = _loc.GetString("Visuals_UnsavedDiscard");
            }
        }
        else
        {
            UnsavedChangesModalIsAppearance = false;
            UnsavedChangesModalSaveLabel = _loc.GetString("Editor_Save");
            UnsavedChangesModalDiscardLabel = _loc.GetString("Editor_UnsavedDiscard");
        }
    }

    private void CloseUnsavedChangesModal(UnsavedChangesPromptResult result)
    {
        _unsavedChangesModalResult = result;
        IsUnsavedChangesModalOpen = false;
        if (_unsavedChangesModalFrame is not null)
            _unsavedChangesModalFrame.Continue = false;
        _unsavedChangesModalFrame = null;
    }

    [RelayCommand]
    private void UnsavedChangesModalSave() => CloseUnsavedChangesModal(UnsavedChangesPromptResult.Save);

    [RelayCommand]
    private void UnsavedChangesModalDiscard() => CloseUnsavedChangesModal(UnsavedChangesPromptResult.Discard);

    [RelayCommand]
    private void UnsavedChangesModalCancel() => CloseUnsavedChangesModal(UnsavedChangesPromptResult.Cancel);

    bool IConfirmModalHost.Confirm(string message, string title)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return false;

        if (!dispatcher.CheckAccess())
            return dispatcher.Invoke(() => ((IConfirmModalHost)this).Confirm(message, title));

        ConfirmModalMessage = message;
        ConfirmModalTitle = title;
        _confirmModalResult = null;
        IsConfirmModalOpen = true;
        _confirmModalFrame = new DispatcherFrame(true);
        System.Windows.Threading.Dispatcher.PushFrame(_confirmModalFrame);
        return _confirmModalResult == true;
    }

    private void CloseConfirmModal(bool yes)
    {
        _confirmModalResult = yes;
        IsConfirmModalOpen = false;
        if (_confirmModalFrame is not null)
            _confirmModalFrame.Continue = false;
        _confirmModalFrame = null;
    }

    [RelayCommand]
    private void ConfirmModalYes() => CloseConfirmModal(true);

    [RelayCommand]
    private void ConfirmModalNo() => CloseConfirmModal(false);

    bool? IEditEventModalHost.ShowEditSingleEventDialog(RecordedInputEvent currentEvent, out RecordedInputEvent updatedEvent)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            updatedEvent = currentEvent;
            return false;
        }

        if (!dispatcher.CheckAccess())
        {
            bool? r = null;
            var ue = currentEvent;
            dispatcher.Invoke(() =>
            {
                r = ShowEditSingleEventDialogOnUiThread(currentEvent, out ue);
            });
            updatedEvent = ue;
            return r;
        }

        return ShowEditSingleEventDialogOnUiThread(currentEvent, out updatedEvent);
    }

    private bool? ShowEditSingleEventDialogOnUiThread(RecordedInputEvent currentEvent, out RecordedInputEvent updatedEvent)
    {
        updatedEvent = currentEvent;
        RecordedInputEvent? built = null;
        var confirmed = RunBlockingContentModal(
            complete => new EditSingleEventView(
                currentEvent,
                _loc,
                msg => _inAppInfo.RequestInfo(msg, _loc.GetString("DialogEditEvent_Title")),
                complete,
                _services.GetRequiredService<ILogger<EditSingleEventView>>()),
            (ok, v) =>
            {
                if (ok && v is EditSingleEventView ev)
                    built = ev.ResultEvent;
            });

        if (confirmed && built is not null)
        {
            updatedEvent = built with
            {
                Sequence = currentEvent.Sequence
            };
            return true;
        }

        return false;
    }

    bool? IEditorInsertModalHost.ShowRenameMacroDialog(string currentName, out string newMacroName)
    {
        newMacroName = currentName;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return false;
        if (!dispatcher.CheckAccess())
        {
            bool? r = null;
            var name = currentName;
            dispatcher.Invoke(() =>
            {
                r = ShowRenameMacroOnUiThread(currentName, out name);
            });
            newMacroName = name;
            return r;
        }

        return ShowRenameMacroOnUiThread(currentName, out newMacroName);
    }

    private bool? ShowRenameMacroOnUiThread(string currentName, out string newMacroName)
    {
        newMacroName = currentName;
        RenameMacroView? view = null;
        var ok = RunBlockingContentModal(
            complete =>
            {
                view = new RenameMacroView(
                    _loc,
                    currentName,
                    msg => _inAppInfo.RequestInfo(msg, _loc.GetString("DialogRename_Title")),
                    complete);
                return view;
            });
        if (ok && view is not null)
        {
            newMacroName = view.NewName;
            return true;
        }

        return false;
    }

    string? IPromptTextModalHost.PromptText(
        string title,
        string message,
        string defaultValue,
        PromptTextValidator? validator,
        bool restrictInputToDigits)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return null;
        if (!dispatcher.CheckAccess())
            return dispatcher.Invoke(() =>
                ((IPromptTextModalHost)this).PromptText(title, message, defaultValue, validator, restrictInputToDigits));

        return PromptTextOnUiThread(title, message, defaultValue, validator, restrictInputToDigits);
    }

    PlaybackKeyChord? IPromptPlaybackChordModalHost.PromptPlaybackChord(
        string title,
        string message,
        IReadOnlyList<PlaybackKeyChord> blockedChords)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return null;
        if (!dispatcher.CheckAccess())
            return dispatcher.Invoke(() =>
                ((IPromptPlaybackChordModalHost)this).PromptPlaybackChord(title, message, blockedChords));

        _playbackHotkeys.Suspend();
        try
        {
            return PromptPlaybackChordOnUiThread(title, message, blockedChords);
        }
        finally
        {
            _playbackHotkeys.Resume();
        }
    }

    private PlaybackKeyChord? PromptPlaybackChordOnUiThread(
        string title,
        string message,
        IReadOnlyList<PlaybackKeyChord> blockedChords)
    {
        PlaybackKeyChord? result = null;
        var ok = RunBlockingContentModal(
            complete => new PromptPlaybackChordView(_loc, title, message, blockedChords, complete),
            (confirmed, v) =>
            {
                if (confirmed && v is PromptPlaybackChordView p)
                    result = p.CapturedChord;
            });
        return ok ? result : null;
    }

    private string? PromptTextOnUiThread(
        string title,
        string message,
        string defaultValue,
        PromptTextValidator? validator,
        bool restrictInputToDigits)
    {
        string? result = null;
        var ok = RunBlockingContentModal(
            complete => new PromptTextView(title, message, defaultValue, complete, validator, restrictInputToDigits),
            (confirmed, v) =>
            {
                if (confirmed && v is PromptTextView p)
                    result = p.ResultText;
            });
        return ok ? result : null;
    }

    /// <summary>Runs a blocking content modal on the UI thread. <paramref name="afterChoice"/> runs before the view is cleared.</summary>
    private bool RunBlockingContentModal(
        Func<Action<bool>, UserControl> createView,
        Action<bool, UserControl?>? afterChoice = null,
        bool focusWhenShown = true)
    {
        if (_contentModalFrame is not null)
            return false;

        var confirmed = false;
        UserControl? viewRef = null;

        void Complete(bool ok)
        {
            if (_contentModalFrame is null)
                return;
            confirmed = ok;
            afterChoice?.Invoke(ok, viewRef);
            IsContentModalOpen = false;
            ContentModalContent = null;
            _contentModalEscapeTarget = null;
            _contentModalFrame.Continue = false;
            _contentModalFrame = null;
        }

        viewRef = createView(Complete);
        _contentModalEscapeTarget = viewRef as IContentModalEscape;
        ContentModalContent = viewRef;
        IsContentModalOpen = true;
        _contentModalFrame = new DispatcherFrame(true);
        if (focusWhenShown)
        {
            var ui = Dispatcher.CurrentDispatcher;
            var v = viewRef;
            ui.BeginInvoke(
                DispatcherPriority.Loaded,
                new System.Action(() =>
                {
                    v.Focus();
                    System.Windows.Input.Keyboard.Focus(v);
                }));
        }

        System.Windows.Threading.Dispatcher.PushFrame(_contentModalFrame);
        return confirmed;
    }

    [RelayCommand]
    private void ContentModalEscape()
    {
        _contentModalEscapeTarget?.CancelFromHost();
    }

    private void OnInAppInfoRequested(object? sender, InAppInfoMessageEventArgs e)
    {
        void Apply()
        {
            InfoModalMessage = e.Message;
            InfoModalTitle = string.IsNullOrWhiteSpace(e.Title) ? "" : e.Title.Trim();
            IsInfoModalOpen = true;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;
        if (dispatcher.CheckAccess())
            Apply();
        else
            dispatcher.Invoke(Apply);
    }

    public bool CanGoBack => _stack.Count > 1;

    public bool ShowBackButton => CanGoBack;

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = _services.GetRequiredService<SettingsViewModel>();
        vm.LoadStateFromPreferences();
        PushPage(vm);
    }

    [RelayCommand]
    private async Task OpenQueueCreator()
    {
        var vm = _services.GetRequiredService<QueueCreatorViewModel>();
        await vm.InitializeAsync().ConfigureAwait(true);
        PushPage(vm);
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task GoBackAsync()
    {
        if (!await TryLeaveTopPageAsync().ConfigureAwait(true))
            return;
        if (_stack.Count <= 1)
            return;
        if (CurrentPage is MacroEditorViewModel oldEditor)
            DetachEditorTitleListener(oldEditor);
        _stack.RemoveAt(_stack.Count - 1);
        CurrentPage = _stack[^1];
        if (ReferenceEquals(CurrentPage, _overview))
            _ = _overview.RefreshAsync(suppressHotkeyRegistrationFailureDialog: true);
        UpdateShellTitle();
        GoBackCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowBackButton));
    }

    internal void PushPage(object page)
    {
        if (CurrentPage is MacroEditorViewModel prevEditor)
            DetachEditorTitleListener(prevEditor);
        _stack.Add(page);
        CurrentPage = page;
        UpdateShellTitle();
        GoBackCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowBackButton));
    }

    internal void ReplaceTop(object page)
    {
        if (_stack.Count == 0)
            return;
        if (CurrentPage is MacroEditorViewModel oldEditor)
            DetachEditorTitleListener(oldEditor);
        _stack[^1] = page;
        CurrentPage = page;
        UpdateShellTitle();
        GoBackCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowBackButton));
    }

    internal async Task<bool> TryLeaveTopPageAsync()
    {
        if (CurrentPage is MacroEditorViewModel editor)
            return await editor.TryLeaveEditorAsync().ConfigureAwait(true) == EditorLeaveResult.Proceed;

        if (CurrentPage is SettingsViewModel settingsVm && settingsVm.HasUnsavedSettingsChanges)
            return settingsVm.TryConfirmLeavePendingSettings();

        if (CurrentPage is QueueCreatorViewModel queueVm && !queueVm.TryConfirmLeaveIfRunning())
            return false;

        return true;
    }

    partial void OnCurrentPageChanged(object? value)
    {
        if (value is MacroEditorViewModel newEditor)
            AttachEditorTitleListener(newEditor);
        else
            DetachEditorTitleListener(_editorTitleSource);
        UpdateShellTitle();
        UpdateShowEditorMacroHeaderActions();
        OnPropertyChanged(nameof(ShowOverviewHeaderChrome));
    }

    private void AttachEditorTitleListener(MacroEditorViewModel editor)
    {
        DetachEditorTitleListener(_editorTitleSource);
        _editorTitleSource = editor;
        editor.PropertyChanged += OnEditorPropertyChanged;
    }

    private void DetachEditorTitleListener(MacroEditorViewModel? editor)
    {
        if (editor is null)
            return;
        editor.PropertyChanged -= OnEditorPropertyChanged;
        if (ReferenceEquals(_editorTitleSource, editor))
            _editorTitleSource = null;
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MacroEditorViewModel.WindowTitle))
            UpdateShellTitle();
        else if (e.PropertyName == nameof(MacroEditorViewModel.EditorHasMacro))
            UpdateShowEditorMacroHeaderActions();
        else if (e.PropertyName == nameof(MacroEditorViewModel.IsRecording))
            RenameEditorMacroCommand.NotifyCanExecuteChanged();
    }

    private void UpdateShellTitle()
    {
        ShellTitle = CurrentPage switch
        {
            MacroEditorViewModel ed => ed.WindowTitle,
            RecordViewModel => _loc.GetString("Record_WindowTitle"),
            SettingsViewModel => _loc.GetString("Main_Settings_PageTitle"),
            QueueCreatorViewModel => _loc.GetString("QueueCreator_WindowTitle"),
            _ => _loc.GetString("Main_WindowTitle")
        };
    }

    [RelayCommand(CanExecute = nameof(CanRenameEditorMacro))]
    private void RenameEditorMacro()
    {
        if (CurrentPage is not MacroEditorViewModel editor)
            return;
        if (!editor.RenameMacroCommand.CanExecute(null))
            return;
        editor.RenameMacroCommand.Execute(null);
    }

    private bool CanRenameEditorMacro() =>
        CurrentPage is MacroEditorViewModel ed && ed.RenameMacroCommand.CanExecute(null);

    [RelayCommand(CanExecute = nameof(CanExportEditorJson))]
    private void ExportEditorJson()
    {
        if (CurrentPage is not MacroEditorViewModel editor || !editor.EditorHasMacro)
            return;
        var json = editor.GetSerializedMacroJson();
        if (string.IsNullOrWhiteSpace(json))
            return;
        var macroName = editor.MacroNameForFileExport ?? "macro";
        ShowExportJsonModalOnUiThread(macroName, json);
    }

    private bool CanExportEditorJson() =>
        CurrentPage is MacroEditorViewModel ed && ed.EditorHasMacro;

    [RelayCommand(CanExecute = nameof(CanDeleteCurrentMacroFromEditor))]
    private async Task DeleteCurrentMacroFromEditorAsync()
    {
        if (CurrentPage is not MacroEditorViewModel editor || !editor.EditorHasMacro)
            return;
        var macroName = editor.MacroNameForFileExport ?? "";
        if (!_dialogs.Confirm(_loc.GetString("Main_DeleteConfirm", macroName)))
            return;
        var workspace = _services.GetRequiredService<MacroWorkspaceService>();
        await workspace.DeleteAsync(editor.MacroId).ConfigureAwait(true);
        ForcePopTopPageSkippingLeaveConfirmation();
        await _overview.RefreshAsync(suppressHotkeyRegistrationFailureDialog: true).ConfigureAwait(true);
    }

    private bool CanDeleteCurrentMacroFromEditor() =>
        CurrentPage is MacroEditorViewModel ed && ed.EditorHasMacro;

    private void ForcePopTopPageSkippingLeaveConfirmation()
    {
        if (_stack.Count <= 1)
            return;
        if (CurrentPage is MacroEditorViewModel oldEditor)
            DetachEditorTitleListener(oldEditor);
        _stack.RemoveAt(_stack.Count - 1);
        CurrentPage = _stack[^1];
        UpdateShellTitle();
        GoBackCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowBackButton));
        UpdateShowEditorMacroHeaderActions();
    }

    void IExportMacroJsonModalHost.ShowExportJsonModal(string macroNameForFileDialog, string json)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ShowExportJsonModalOnUiThread(macroNameForFileDialog, json));
            return;
        }

        ShowExportJsonModalOnUiThread(macroNameForFileDialog, json);
    }

    private void ShowExportJsonModalOnUiThread(string macroNameForFileDialog, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;
        var macroName = string.IsNullOrWhiteSpace(macroNameForFileDialog) ? "macro" : macroNameForFileDialog.Trim();
        RunBlockingContentModal(complete => new MacroJsonShareView(
            _loc,
            _dialogs,
            macroName,
            json,
            complete,
            _services.GetRequiredService<ILogger<MacroJsonShareView>>()));
    }

    void IImportMacroJsonModalHost.ShowImportMacroModal(Func<string, Task<bool>> importJsonAsync)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ShowImportMacroModalOnUiThread(importJsonAsync));
            return;
        }

        ShowImportMacroModalOnUiThread(importJsonAsync);
    }

    private void ShowImportMacroModalOnUiThread(Func<string, Task<bool>> importJsonAsync) =>
        RunBlockingContentModal(complete => new MacroJsonImportView(
            _loc,
            _dialogs,
            importJsonAsync,
            complete,
            _services.GetRequiredService<ILogger<MacroJsonImportView>>()));

    UpdatePromptChoice IUpdatePromptModalHost.ShowUpdateAvailable(UpdateCheckResult result)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return UpdatePromptChoice.Later;

        if (!dispatcher.CheckAccess())
            return dispatcher.Invoke(() => ((IUpdatePromptModalHost)this).ShowUpdateAvailable(result));

        var applyNow = RunBlockingContentModal(
            complete => new UpdateAvailableView(_loc, result, complete));
        return applyNow ? UpdatePromptChoice.ApplyNow : UpdatePromptChoice.Later;
    }

    private void UpdateShowEditorMacroHeaderActions()
    {
        ShowEditorMacroHeaderActions = CurrentPage is MacroEditorViewModel ed && ed.EditorHasMacro;
        ExportEditorJsonCommand.NotifyCanExecuteChanged();
        DeleteCurrentMacroFromEditorCommand.NotifyCanExecuteChanged();
        RenameEditorMacroCommand.NotifyCanExecuteChanged();
    }

    internal MacroEditorViewModel CreateEditorViewModel(
        MacroId macroId,
        bool loadFromDisk,
        Macro? inMemoryMacro,
        Action? onMacroSaved) =>
        new MacroEditorViewModel(
            _services.GetRequiredService<MacroWorkspaceService>(),
            _services.GetRequiredService<IUserDialogService>(),
            _services.GetRequiredService<IEditorInsertDialogs>(),
            _services.GetRequiredService<IPlaybackService>(),
            _services.GetRequiredService<RecordingCoordinator>(),
            _services.GetRequiredService<IUiLocalizer>(),
            _services.GetRequiredService<InAppInfoMessageChannel>(),
            _services.GetRequiredService<ILogger<MacroEditorViewModel>>(),
            macroId,
            loadFromDisk,
            inMemoryMacro,
            onMacroSaved);
}
