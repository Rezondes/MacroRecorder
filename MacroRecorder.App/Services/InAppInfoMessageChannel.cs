namespace MacroRecorder.App.Services;

public sealed class InAppInfoMessageEventArgs(string message, string? title) : EventArgs
{
    public string Message { get; } = message;

    public string? Title { get; } = title;
}

public sealed class InAppInfoMessageChannel
{
    public event EventHandler<InAppInfoMessageEventArgs>? InfoRequested;

    public void RequestInfo(string message, string? title = null) =>
        InfoRequested?.Invoke(this, new InAppInfoMessageEventArgs(message, title));
}
