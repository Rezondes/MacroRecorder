using MacroRecorder.Application.MacroQueue;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

/// <summary>Manual pause between queue steps (blocks <see cref="MacroQueueRunner"/> until resumed).</summary>
public sealed class MacroQueuePauseController : IMacroQueuePauseSignal
{
    private readonly object _sync = new();
    private bool _paused;
    private TaskCompletionSource _gate = CreateGate();

    private static TaskCompletionSource CreateGate() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsPaused
    {
        get
        {
            lock (_sync)
                return _paused;
        }
    }

    public void SetPaused(bool paused)
    {
        lock (_sync)
        {
            if (paused)
            {
                _paused = true;
                _gate = CreateGate();
            }
            else
            {
                _paused = false;
                _gate.TrySetResult();
            }
        }
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task waitTask;
        lock (_sync)
        {
            if (!_paused)
                return;
            waitTask = _gate.Task;
        }

        await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        await WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
    }
}
