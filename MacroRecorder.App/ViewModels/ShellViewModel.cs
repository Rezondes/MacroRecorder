using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MacroRecorder.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly MainViewModel _overview;
    private readonly IServiceProvider _services;
    private readonly IUiLocalizer _loc;
    private readonly InAppInfoMessageChannel _inAppInfo;
    private readonly List<object> _stack = new();
    private MacroEditorViewModel? _editorTitleSource;

    public ShellViewModel(
        MainViewModel overview,
        IServiceProvider services,
        IUiLocalizer loc,
        InAppInfoMessageChannel inAppInfo)
    {
        _overview = overview;
        _services = services;
        _loc = loc;
        _inAppInfo = inAppInfo;
        _inAppInfo.InfoRequested += OnInAppInfoRequested;
        _stack.Add(overview);
        CurrentPage = overview;
        UpdateShellTitle();
    }

    public MainViewModel Overview => _overview;

    [ObservableProperty]
    private object? currentPage;

    [ObservableProperty]
    private bool isSettingsOpen;

    [ObservableProperty]
    private bool isInfoModalOpen;

    [ObservableProperty]
    private string infoModalTitle = "";

    [ObservableProperty]
    private string infoModalMessage = "";

    [ObservableProperty]
    private string shellTitle = "";

    [RelayCommand]
    private void CloseInfoModal() => IsInfoModalOpen = false;

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
    private void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

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
        return true;
    }

    partial void OnCurrentPageChanged(object? value)
    {
        if (value is MacroEditorViewModel newEditor)
            AttachEditorTitleListener(newEditor);
        else
            DetachEditorTitleListener(_editorTitleSource);
        UpdateShellTitle();
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
    }

    private void UpdateShellTitle()
    {
        ShellTitle = CurrentPage switch
        {
            MacroEditorViewModel ed => ed.WindowTitle,
            RecordViewModel => _loc.GetString("Record_WindowTitle"),
            _ => _loc.GetString("Main_WindowTitle")
        };
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
