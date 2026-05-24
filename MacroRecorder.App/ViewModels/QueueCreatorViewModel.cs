using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Localization;
using MacroRecorder.App.Services;
using MacroRecorder.Application;
using MacroRecorder.Application.MacroQueue;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using MacroRecorder.Domain.MacroQueue;
using MacroRecorder.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace MacroRecorder.App.ViewModels;

public partial class QueueCreatorViewModel : ObservableObject
{
    private readonly MacroWorkspaceService _workspace;
    private readonly IPlaybackService _playback;
    private readonly IUserDialogService _dialogs;
    private readonly IUiLocalizer _loc;
    private readonly MacroQueueFileStore _queueFiles;
    private readonly InAppInfoMessageChannel _inApp;
    private readonly ILogger<QueueCreatorViewModel> _logger;
    private readonly MacroQueuePauseController _pause = new();
    private CancellationTokenSource? _runCts;
    private HashSet<MacroId> _knownMacroIds = new();

    public QueueCreatorViewModel(
        MacroWorkspaceService workspace,
        IPlaybackService playback,
        IUserDialogService dialogs,
        IUiLocalizer loc,
        MacroQueueFileStore queueFiles,
        InAppInfoMessageChannel inApp,
        ILogger<QueueCreatorViewModel> logger)
    {
        _workspace = workspace;
        _playback = playback;
        _dialogs = dialogs;
        _loc = loc;
        _queueFiles = queueFiles;
        _inApp = inApp;
        _logger = logger;
        _loc.UiCultureChanged += (_, _) =>
        {
            RefreshDerivedState();
            OnPropertyChanged(nameof(PauseToggleLabel));
        };
        Steps.CollectionChanged += OnStepsCollectionChanged;
    }

    public ObservableCollection<MacroSummary> AvailableMacros { get; } = new();

    public ObservableCollection<QueueStepRowViewModel> Steps { get; } = new();

    [ObservableProperty]
    private QueueStepRowViewModel? selectedStep;

    [ObservableProperty]
    private string queueName = "";

    [ObservableProperty]
    private bool loopWholeQueue;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string estimatedDurationText = "";

    [ObservableProperty]
    private string? currentFilePath;

    public ObservableCollection<string> TimelinePreviewLines { get; } = new();

    public bool IsPaused => _pause.IsPaused;

    public string PauseToggleLabel =>
        IsPaused ? _loc.GetString("QueueCreator_Resume") : _loc.GetString("QueueCreator_Pause");

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(PauseToggleLabel));

    partial void OnLoopWholeQueueChanged(bool value) => RefreshDerivedState();

    partial void OnQueueNameChanged(string value) => RefreshDerivedState();

    private void OnStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RefreshDerivedState();

    public async Task InitializeAsync()
    {
        await RefreshMacrosAsync().ConfigureAwait(true);
        if (Steps.Count == 0)
            AddStep();
    }

    /// <summary>Adds a queue step for the given macro (e.g. drag-and-drop from the macro list).</summary>
    public void AddStepForMacro(MacroId macroId)
    {
        var row = new QueueStepRowViewModel(RefreshDerivedState, macroId, 1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        Steps.Add(row);
        SelectedStep = row;
        RefreshDerivedState();
    }

    /// <summary>Shell navigation: confirm before leaving the page while a queue run is active.</summary>
    public bool TryConfirmLeaveIfRunning()
    {
        if (!IsRunning)
            return true;
        if (!_dialogs.Confirm(_loc.GetString("QueueCreator_ConfirmLeaveWhileRunning")))
            return false;
        _runCts?.Cancel();
        _playback.RequestUserCancel();
        return true;
    }

    private async Task RefreshMacrosAsync()
    {
        var list = await _workspace.ListAsync().ConfigureAwait(true);
        AvailableMacros.Clear();
        _knownMacroIds = new HashSet<MacroId>();
        foreach (var summary in list)
        {
            AvailableMacros.Add(summary);
            _knownMacroIds.Add(summary.Id);
        }

        RefreshDerivedState();
    }

    private void RefreshDerivedState()
    {
        foreach (var row in Steps)
            row.MacroMissing = !_knownMacroIds.Contains(row.MacroId);

        var document = BuildDocumentLenient();
        if (document is null || document.Steps.Count == 0)
        {
            EstimatedDurationText = "";
            TimelinePreviewLines.Clear();
            return;
        }

        var durationMap = MacroQueueRunner.PlaybackDurationsFromSummaries(AvailableMacros);
        var estimate = MacroQueueRunner.EstimateTotalDuration(document, durationMap);
        EstimatedDurationText = estimate <= TimeSpan.Zero
            ? ""
            : _loc.GetString("QueueCreator_EstimatedDurationFormat", FormatDuration(estimate));

        TimelinePreviewLines.Clear();
        foreach (var line in BuildTimelinePreview(document, durationMap))
            TimelinePreviewLines.Add(line);
    }

    private MacroQueueDocument? BuildDocumentLenient()
    {
        if (Steps.Count == 0)
            return null;
        var list = Steps.Select(static row => row.ToQueueStep()).ToList();
        return MacroQueueDocument.Create(QueueName.Trim(), list, LoopWholeQueue);
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalDays >= 1)
            return $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m";
        if (value.TotalHours >= 1)
            return $"{(int)value.TotalHours}h {value.Minutes}m {value.Seconds}s";
        if (value.TotalMinutes >= 1)
            return $"{(int)value.TotalMinutes}m {value.Seconds}s";
        return $"{value.TotalSeconds:0.###}s";
    }

    private IEnumerable<string> BuildTimelinePreview(MacroQueueDocument document, IReadOnlyDictionary<MacroId, TimeSpan> durationMap)
    {
        var offset = TimeSpan.Zero;
        foreach (var step in document.Steps)
        {
            offset += step.InitialDelay;
            var macroName = ResolveMacroName(step.MacroId);
            for (var run = 1; run <= step.RepeatCount; run++)
            {
                yield return _loc.GetString(
                    "QueueCreator_TimelineLine",
                    FormatDuration(offset),
                    macroName,
                    run,
                    step.RepeatCount);
                if (!durationMap.TryGetValue(step.MacroId, out var playOnce))
                    playOnce = TimeSpan.Zero;
                offset += playOnce;
                if (run < step.RepeatCount)
                    offset += step.DelayBetweenRuns;
            }

            offset += step.PostStepDelay;
        }
    }

    private string ResolveMacroName(MacroId id)
    {
        foreach (var summary in AvailableMacros)
        {
            if (summary.Id == id)
                return summary.Name;
        }

        return id.Value;
    }

    private MacroQueueDocument? TryBuildDocumentForSave(out string errorMessage)
    {
        errorMessage = "";
        var list = new List<QueueStep>();
        foreach (var row in Steps)
        {
            if (!row.TryParseDelays(_loc, out errorMessage))
                return null;
            list.Add(row.ToQueueStep());
        }

        return MacroQueueDocument.Create(QueueName.Trim(), list, LoopWholeQueue);
    }

    [RelayCommand]
    private void AddStep()
    {
        var defaultId = AvailableMacros.Count > 0 ? AvailableMacros[0].Id : MacroId.New();
        var row = new QueueStepRowViewModel(RefreshDerivedState, defaultId, 1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        Steps.Add(row);
        SelectedStep = row;
    }

    [RelayCommand]
    private void RemoveStep(QueueStepRowViewModel? row)
    {
        row ??= SelectedStep;
        if (row is null)
            return;
        var index = Steps.IndexOf(row);
        Steps.Remove(row);
        if (Steps.Count == 0)
        {
            SelectedStep = null;
            return;
        }

        SelectedStep = Steps[Math.Clamp(index, 0, Steps.Count - 1)];
    }

    [RelayCommand]
    private void DuplicateStep(QueueStepRowViewModel? row)
    {
        row ??= SelectedStep;
        if (row is null)
            return;
        var index = Steps.IndexOf(row);
        var clone = row.Clone(RefreshDerivedState);
        Steps.Insert(index + 1, clone);
        SelectedStep = clone;
    }

    [RelayCommand]
    private void MoveStepUp(QueueStepRowViewModel? row)
    {
        row ??= SelectedStep;
        if (row is null)
            return;
        var index = Steps.IndexOf(row);
        if (index <= 0)
            return;
        Steps.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveStepDown(QueueStepRowViewModel? row)
    {
        row ??= SelectedStep;
        if (row is null)
            return;
        var index = Steps.IndexOf(row);
        if (index < 0 || index >= Steps.Count - 1)
            return;
        Steps.Move(index, index + 1);
    }

    [RelayCommand]
    private async Task SaveQueueAsync()
    {
        var document = TryBuildDocumentForSave(out var parseError);
        if (document is null)
        {
            _dialogs.ShowInfo(parseError);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = _loc.GetString("QueueCreator_FileFilter"),
            FileName = string.IsNullOrWhiteSpace(QueueName) ? "queue" : QueueName.Trim(),
            InitialDirectory = _queueFiles.RootDirectory,
            AddExtension = true,
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _queueFiles.SaveAsync(dialog.FileName, document).ConfigureAwait(true);
            CurrentFilePath = dialog.FileName;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save macro queue");
            _dialogs.ShowInfo(_loc.GetString("QueueCreator_ErrorSave", exception.Message));
        }
    }

    [RelayCommand]
    private async Task LoadQueueAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = _loc.GetString("QueueCreator_FileFilter"),
            InitialDirectory = _queueFiles.RootDirectory
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var document = await _queueFiles.LoadAsync(dialog.FileName).ConfigureAwait(true);
            ApplyDocument(document);
            CurrentFilePath = dialog.FileName;
            await RefreshMacrosAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load macro queue");
            _dialogs.ShowInfo(_loc.GetString("QueueCreator_ErrorLoad", exception.Message));
        }
    }

    private void ApplyDocument(MacroQueueDocument document)
    {
        Steps.Clear();
        QueueName = document.Name;
        LoopWholeQueue = document.LoopWholeQueue;
        foreach (var step in document.Steps)
        {
            var row = new QueueStepRowViewModel(
                RefreshDerivedState,
                step.MacroId,
                step.RepeatCount,
                step.InitialDelay,
                step.DelayBetweenRuns,
                step.PostStepDelay);
            Steps.Add(row);
        }

        SelectedStep = Steps.Count > 0 ? Steps[0] : null;
        RefreshDerivedState();
    }

    [RelayCommand]
    private async Task RunQueueAsync()
    {
        if (IsRunning)
            return;

        if (Steps.Any(static row => row.MacroMissing))
        {
            if (!_dialogs.Confirm(_loc.GetString("QueueCreator_ConfirmMissingMacros")))
                return;
        }

        var document = TryBuildDocumentForSave(out var parseError);
        if (document is null)
        {
            _dialogs.ShowInfo(parseError);
            return;
        }

        if (document.Steps.Count == 0)
        {
            _dialogs.ShowInfo(_loc.GetString("QueueCreator_ErrorNoSteps"));
            return;
        }

        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;
        IsRunning = true;
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(PauseToggleLabel));
        _pause.SetPaused(false);

        try
        {
            var runner = new MacroQueueRunner(_workspace, _playback, _pause);
            var prefs = AppSettingsStore.Load();
            await runner.RunAsync(
                document,
                prefs.PlaybackUserInterruptGraceMs,
                prefs.PlaybackFocusBringWindowToForeground,
                prefs.PlaybackFocusRestoreIfMinimized,
                token).ConfigureAwait(true);
        }
        catch (PlaybackAbortedByUserRequestException)
        {
            // overlay cancel during start delay
        }
        catch (PlaybackInterruptedByUserException)
        {
            _inApp.RequestInfo(
                _loc.GetString("Main_Play_InterruptedByUser"),
                _loc.GetString("Main_PlaybackInterruptedModalTitle"));
        }
        catch (MacroQueueMissingMacroException missingMacroException)
        {
            _dialogs.ShowInfo(_loc.GetString("QueueCreator_ErrorMissingMacro", missingMacroException.MacroId.Value));
        }
        catch (MacroQueueEmptyMacroException emptyMacroException)
        {
            _dialogs.ShowInfo(_loc.GetString("QueueCreator_ErrorEmptyMacro", emptyMacroException.MacroId.Value));
        }
        catch (PlaybackFocusTargetException focusTargetException)
        {
            _dialogs.ShowInfo(PlaybackFocusTargetUi.FormatMessage(_loc, focusTargetException));
        }
        catch (InvalidOperationException)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorPlaybackFailed"));
        }
        catch (OperationCanceledException)
        {
            // queue cancelled (token) or other cooperative cancellation
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Macro queue playback failed");
            _dialogs.ShowInfo(_loc.GetString("Main_Play_ErrorDetail", exception.Message));
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            IsRunning = false;
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(PauseToggleLabel));
        }
    }

    [RelayCommand]
    private void StopQueue()
    {
        _runCts?.Cancel();
        _playback.RequestUserCancel();
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (!IsRunning)
            return;
        _pause.SetPaused(!_pause.IsPaused);
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(PauseToggleLabel));
    }
}
