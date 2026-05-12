namespace MacroRecorder.Domain;

public sealed class Macro
{
    private readonly List<RecordedInputEvent> _events = new();

    public Macro(
        MacroId id,
        string name,
        RecordingMetadata metadata,
        IEnumerable<RecordedInputEvent>? events = null,
        bool wasModifiedAfterRecording = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Id = id;
        Name = name.Trim();
        Metadata = metadata;
        WasModifiedAfterRecording = wasModifiedAfterRecording;
        if (events is not null)
            _events.AddRange(events);
    }

    public MacroId Id { get; }
    public string Name { get; private set; }
    public RecordingMetadata Metadata { get; private set; }
    public IReadOnlyList<RecordedInputEvent> Events => _events;
    public bool WasModifiedAfterRecording { get; private set; }

    public static Macro CreateEmpty(string name) =>
        new(MacroId.New(), name, RecordingMetadata.Empty());

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));
        Name = newName.Trim();
        WasModifiedAfterRecording = true;
    }

    public void SetMetadata(RecordingMetadata metadata) => Metadata = metadata;

    public void AppendEvent(RecordedInputEvent e) => _events.Add(e);

    public void AppendEvents(IEnumerable<RecordedInputEvent> items) => _events.AddRange(items);

    public void ReplaceEvents(IReadOnlyList<RecordedInputEvent> events, bool markEdited = true)
    {
        _events.Clear();
        _events.AddRange(events);
        if (markEdited)
            WasModifiedAfterRecording = true;
    }

    public void RemoveEventAt(int index)
    {
        _events.RemoveAt(index);
        WasModifiedAfterRecording = true;
    }

    public void InsertEvent(int index, RecordedInputEvent e)
    {
        _events.Insert(index, e);
        WasModifiedAfterRecording = true;
    }

    public void ReplaceEventAt(int index, RecordedInputEvent e)
    {
        _events[index] = e;
        WasModifiedAfterRecording = true;
    }

    public void MarkRecordedClean() => WasModifiedAfterRecording = false;
}
