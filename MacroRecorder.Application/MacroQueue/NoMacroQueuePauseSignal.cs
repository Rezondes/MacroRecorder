namespace MacroRecorder.Application.MacroQueue;

public sealed class NoMacroQueuePauseSignal : IMacroQueuePauseSignal
{
    public static readonly NoMacroQueuePauseSignal Instance = new();

    public Task WaitIfPausedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
