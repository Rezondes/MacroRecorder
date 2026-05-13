using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Editor;

public abstract class EditorTimelineRow
{
    public abstract string ActionLabel { get; }
    public abstract string ValueText { get; }
    /// <summary>Segoe MDL2 Assets glyph (one character) for the icon column.</summary>
    public abstract string TimelineIconGlyph { get; }
}

public sealed class EditorSingleEventRow : EditorTimelineRow
{
    private readonly IUiLocalizer _loc;

    public EditorSingleEventRow(RecordedInputEvent e, IUiLocalizer loc)
    {
        Event = e;
        _loc = loc;
    }

    public RecordedInputEvent Event { get; }

    public override string ActionLabel => EditorEventFormatter.ActionLabel(Event, _loc);

    public override string ValueText => EditorEventFormatter.ValueText(Event, _loc);

    public override string TimelineIconGlyph => EditorEventFormatter.TimelineIconGlyph(Event);
}

public sealed class EditorMousePathRow : EditorTimelineRow
{
    private readonly IUiLocalizer _loc;

    public EditorMousePathRow(List<MouseMoveRecordedEvent> moves, IUiLocalizer loc)
    {
        Moves = moves;
        _loc = loc;
    }

    public IReadOnlyList<MouseMoveRecordedEvent> Moves { get; }

    public override string ActionLabel => _loc.GetString("Editor_Event_MousePath");

    public override string TimelineIconGlyph => "\uEC87";

    public override string ValueText
    {
        get
        {
            if (Moves.Count == 0)
                return "";
            var first = Moves[0];
            var last = Moves[^1];
            var span = EventPlaybackSchedule.PlaybackSpanOverSublist(Moves);
            return _loc.GetString(
                "Editor_MousePathValueFormat",
                Moves.Count,
                span.TotalMilliseconds,
                first.ScreenX,
                first.ScreenY,
                last.ScreenX,
                last.ScreenY);
        }
    }
}
