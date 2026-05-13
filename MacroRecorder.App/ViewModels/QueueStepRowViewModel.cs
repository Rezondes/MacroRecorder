using CommunityToolkit.Mvvm.ComponentModel;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using MacroRecorder.Domain.MacroQueue;

namespace MacroRecorder.App.ViewModels;

public sealed partial class QueueStepRowViewModel : ObservableObject
{
    private readonly Action _onEdited;

    public QueueStepRowViewModel(Action onEdited, MacroId macroId, int repeatCount, TimeSpan initialDelay, TimeSpan delayBetweenRuns, TimeSpan postStepDelay)
    {
        _onEdited = onEdited;
        _macroId = macroId;
        _repeatCountText = repeatCount < 1 ? "1" : repeatCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _initialDelayText = FormatDelay(initialDelay);
        _delayBetweenRunsText = FormatDelay(delayBetweenRuns);
        _postStepDelayText = FormatDelay(postStepDelay);
    }

    [ObservableProperty]
    private MacroId _macroId;

    [ObservableProperty]
    private string _repeatCountText = "1";

    [ObservableProperty]
    private string _initialDelayText = "0:0:0";

    [ObservableProperty]
    private string _delayBetweenRunsText = "0:0:0";

    [ObservableProperty]
    private string _postStepDelayText = "0:0:0";

    [ObservableProperty]
    private bool _macroMissing;

    partial void OnMacroIdChanged(MacroId value) => _onEdited();

    partial void OnRepeatCountTextChanged(string value) => _onEdited();

    partial void OnInitialDelayTextChanged(string value) => _onEdited();

    partial void OnDelayBetweenRunsTextChanged(string value) => _onEdited();

    partial void OnPostStepDelayTextChanged(string value) => _onEdited();

    public QueueStep ToQueueStep()
    {
        var repeat = ParseRepeatCount(RepeatCountText);
        return new QueueStep(
            MacroId,
            repeat,
            ParseDelayOrZero(InitialDelayText),
            ParseDelayOrZero(DelayBetweenRunsText),
            ParseDelayOrZero(PostStepDelayText));
    }

    private static int ParseRepeatCount(string text)
    {
        if (int.TryParse(text, out var value) && value >= 1)
            return value;
        return 1;
    }

    public bool TryParseDelays(IUiLocalizer loc, out string errorMessage)
    {
        if (!int.TryParse(RepeatCountText.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var repeatParsed) || repeatParsed < 1)
        {
            errorMessage = loc.GetString("QueueCreator_ErrorInvalidRepeat");
            return false;
        }

        if (!TryParseDelay(InitialDelayText, out _))
        {
            errorMessage = loc.GetString("QueueCreator_ErrorInvalidTime");
            return false;
        }

        if (!TryParseDelay(DelayBetweenRunsText, out _))
        {
            errorMessage = loc.GetString("QueueCreator_ErrorInvalidTime");
            return false;
        }

        if (!TryParseDelay(PostStepDelayText, out _))
        {
            errorMessage = loc.GetString("QueueCreator_ErrorInvalidTime");
            return false;
        }

        errorMessage = "";
        return true;
    }

    public QueueStepRowViewModel Clone(Action onEdited) =>
        new(onEdited, MacroId, ParseRepeatCount(RepeatCountText),
            ParseDelayOrZero(InitialDelayText),
            ParseDelayOrZero(DelayBetweenRunsText),
            ParseDelayOrZero(PostStepDelayText));

    public static string FormatDelay(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;
        return $"{(int)value.TotalHours}:{value.Minutes}:{value.Seconds}";
    }

    private static TimeSpan ParseDelayOrZero(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return TimeSpan.Zero;
        return TryParseDelay(text.Trim(), out var parsed) ? parsed : TimeSpan.Zero;
    }

    private static bool TryParseDelay(string text, out TimeSpan value)
    {
        if (TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            if (value < TimeSpan.Zero)
                value = TimeSpan.Zero;
            return true;
        }

        value = TimeSpan.Zero;
        return false;
    }
}
