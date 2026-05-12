using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

/// <summary>Edit-single-event UI hosted on the shell instead of a separate <see cref="System.Windows.Window"/>.</summary>
public interface IEditEventModalHost
{
    bool? ShowEditSingleEventDialog(RecordedInputEvent currentEvent, out RecordedInputEvent updatedEvent);
}
