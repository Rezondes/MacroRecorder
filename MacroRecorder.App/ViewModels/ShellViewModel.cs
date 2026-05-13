using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.App.Views.Editor;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MacroRecorder.App.ViewModels;

public partial class ShellViewModel : ObservableObject,
    IUnsavedChangesModalHost,
    IConfirmModalHost,
    IEditEventModalHost,
    IEditorInsertModalHost,
    IPromptTextModalHost,
    IPlaybackUiFeedback
{
    private readonly MainViewModel _overview;
    private readonly IServiceProvider _services;
    private readonly IUiLocalizer _loc;
    private readonly IUserDialogService _dialogs;
    private readonly InAppInfoMessageChannel _inAppInfo;
    private readonly List<object> _stack = new();
    private MacroEditorViewModel? _editorTitleSource;

    public ShellViewModel(
        MainViewModel overview,
        IServiceProvider services,
        IUiLocalizer loc,
        IUserDialogService dialogs,
        InAppInfoMessageChannel inAppInfo)
    {
        _overview = overview;
        _services = services;
        _loc = loc;
        _dialogs = dialogs;
        _inAppInfo = inAppInfo;
        _inAppInfo.InfoRequested += OnInAppInfoRequested;
        _loc.UiCultureChanged += (_, _) => UpdateShellTitle();
        _stack.Add(overview);
        CurrentPage = overview;
        UpdateShellTitle();
        UpdateShowEditorShareJsonButton();
    }

    public MainViewModel Overview => _overview;

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
    private bool showEditorShareJsonButton;

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
                complete),
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

    bool? IEditorInsertModalHost.ShowInsertMouseClickDialog(out int screenX, out int screenY, out MouseButtonKind mouseButton)
    {
        screenX = 0;
        screenY = 0;
        mouseButton = MouseButtonKind.Left;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return false;
        if (!dispatcher.CheckAccess())
        {
            bool? r = null;
            var sx = 0;
            var sy = 0;
            var mb = MouseButtonKind.Left;
            dispatcher.Invoke(() =>
            {
                r = ShowInsertMouseClickOnUiThread(out sx, out sy, out mb);
            });
            screenX = sx;
            screenY = sy;
            mouseButton = mb;
            return r;
        }

        return ShowInsertMouseClickOnUiThread(out screenX, out screenY, out mouseButton);
    }

    private bool? ShowInsertMouseClickOnUiThread(out int screenX, out int screenY, out MouseButtonKind mouseButton)
    {
        screenX = 0;
        screenY = 0;
        mouseButton = MouseButtonKind.Left;
        InsertMouseClickView? view = null;
        var ok = RunBlockingContentModal(
            complete =>
            {
                view = new InsertMouseClickView(complete);
                return view;
            });
        if (ok && view is not null)
        {
            screenX = view.ScreenX;
            screenY = view.ScreenY;
            mouseButton = view.SelectedButton;
            return true;
        }

        return false;
    }

    bool? IEditorInsertModalHost.ShowInsertKeyStrokeDialog(out ushort virtualKey, out ushort scanCode, out bool isExtendedKey)
    {
        virtualKey = 0;
        scanCode = 0;
        isExtendedKey = false;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return false;
        if (!dispatcher.CheckAccess())
        {
            bool? r = null;
            ushort vk = 0, sc = 0;
            var ext = false;
            dispatcher.Invoke(() =>
            {
                r = ShowInsertKeyStrokeOnUiThread(out vk, out sc, out ext);
            });
            virtualKey = vk;
            scanCode = sc;
            isExtendedKey = ext;
            return r;
        }

        return ShowInsertKeyStrokeOnUiThread(out virtualKey, out scanCode, out isExtendedKey);
    }

    private bool? ShowInsertKeyStrokeOnUiThread(out ushort virtualKey, out ushort scanCode, out bool isExtendedKey)
    {
        virtualKey = 0;
        scanCode = 0;
        isExtendedKey = false;
        InsertKeyStrokeView? view = null;
        var ok = RunBlockingContentModal(
            complete =>
            {
                view = new InsertKeyStrokeView(
                    _loc,
                    msg => _inAppInfo.RequestInfo(msg, _loc.GetString("DialogInsertKey_Title")),
                    complete);
                return view;
            });
        if (ok && view is not null)
        {
            virtualKey = view.CapturedVk;
            scanCode = view.CapturedScan;
            isExtendedKey = view.CapturedExtended;
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
            _ = _overview.RefreshAsync();
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

        return true;
    }

    partial void OnCurrentPageChanged(object? value)
    {
        if (value is MacroEditorViewModel newEditor)
            AttachEditorTitleListener(newEditor);
        else
            DetachEditorTitleListener(_editorTitleSource);
        UpdateShellTitle();
        UpdateShowEditorShareJsonButton();
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
            UpdateShowEditorShareJsonButton();
    }

    private void UpdateShellTitle()
    {
        ShellTitle = CurrentPage switch
        {
            MacroEditorViewModel ed => ed.WindowTitle,
            RecordViewModel => _loc.GetString("Record_WindowTitle"),
            SettingsViewModel => _loc.GetString("Main_Settings_PageTitle"),
            _ => _loc.GetString("Main_WindowTitle")
        };
    }

    [RelayCommand(CanExecute = nameof(CanShareEditorJson))]
    private void ShareEditorJson()
    {
        if (CurrentPage is not MacroEditorViewModel editor || !editor.EditorHasMacro)
            return;
        var json = editor.GetSerializedMacroJson();
        if (string.IsNullOrWhiteSpace(json))
            return;
        var macroName = editor.MacroNameForFileExport ?? "macro";
        RunBlockingContentModal(complete => new MacroJsonShareView(_loc, _dialogs, macroName, json, complete));
    }

    private bool CanShareEditorJson() =>
        CurrentPage is MacroEditorViewModel ed && ed.EditorHasMacro;

    private void UpdateShowEditorShareJsonButton()
    {
        ShowEditorShareJsonButton = CurrentPage is MacroEditorViewModel ed && ed.EditorHasMacro;
        ShareEditorJsonCommand.NotifyCanExecuteChanged();
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
            macroId,
            loadFromDisk,
            inMemoryMacro,
            onMacroSaved);
}
