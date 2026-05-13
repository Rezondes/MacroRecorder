namespace MacroRecorder.Domain;

public sealed class Macro
{
    private readonly List<RecordedInputEvent> _events = new();

    public Macro(
        MacroId id,
        string name,
        RecordingMetadata metadata,
        IEnumerable<RecordedInputEvent>? events = null,
        bool wasModifiedAfterRecording = false,
        Ulid? documentVersion = null,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? lastModifiedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Id = id;
        Name = name.Trim();
        Metadata = metadata;
        WasModifiedAfterRecording = wasModifiedAfterRecording;
        DocumentVersion = documentVersion ?? Ulid.NewUlid();
        var now = DateTimeOffset.UtcNow;
        CreatedAtUtc = createdAtUtc ?? now;
        LastModifiedAtUtc = lastModifiedAtUtc ?? now;
        if (events is not null)
            _events.AddRange(events);
    }

    public MacroId Id { get; }
    public string Name { get; private set; }
    public RecordingMetadata Metadata { get; private set; }
    public IReadOnlyList<RecordedInputEvent> Events => _events;
    public bool WasModifiedAfterRecording { get; private set; }

    /// <summary>ULID that changes whenever macro <b>content</b> changes (events, name, recording metadata).</summary>
    public Ulid DocumentVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastModifiedAtUtc { get; private set; }

    public static Macro CreateEmpty(string name) =>
        new(MacroId.New(), name, RecordingMetadata.Empty());

    private void TouchStructuralChange()
    {
        DocumentVersion = Ulid.NewUlid();
        LastModifiedAtUtc = DateTimeOffset.UtcNow;
    }

    public void AssignNameOnly(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));
        Name = newName.Trim();
        WasModifiedAfterRecording = true;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));
        Name = newName.Trim();
        WasModifiedAfterRecording = true;
        TouchStructuralChange();
    }

    public void SetMetadata(RecordingMetadata metadata)
    {
        Metadata = metadata;
        TouchStructuralChange();
    }

    /// <summary>Replaces the flat timeline after recording merge (single structural bump).</summary>
    public void ApplyRecordingMerge(IReadOnlyList<RecordedInputEvent> events, RecordingMetadata metadata)
    {
        _events.Clear();
        _events.AddRange(events);
        Metadata = metadata;
        WasModifiedAfterRecording = true;
        TouchStructuralChange();
    }

    /// <summary>Persists editor timeline to the macro model (e.g. on save).</summary>
    public void ApplyPersistedEditorState(
        IReadOnlyList<RecordedInputEvent> eventsInOrder,
        bool markRecordedDirty,
        bool bumpDocumentVersion)
    {
        _events.Clear();
        _events.AddRange(eventsInOrder);
        if (markRecordedDirty)
            WasModifiedAfterRecording = true;
        LastModifiedAtUtc = DateTimeOffset.UtcNow;
        if (bumpDocumentVersion)
            DocumentVersion = Ulid.NewUlid();
    }

    public void AppendEvent(RecordedInputEvent e) => _events.Add(e);

    public void AppendEvents(IEnumerable<RecordedInputEvent> items) => _events.AddRange(items);

    public void RemoveEventAt(int index)
    {
        _events.RemoveAt(index);
        WasModifiedAfterRecording = true;
        TouchStructuralChange();
    }

    public void InsertEvent(int index, RecordedInputEvent e)
    {
        _events.Insert(index, e);
        WasModifiedAfterRecording = true;
        TouchStructuralChange();
    }

    public void ReplaceEventAt(int index, RecordedInputEvent e)
    {
        _events[index] = e;
        WasModifiedAfterRecording = true;
        TouchStructuralChange();
    }

    public void MarkRecordedClean() => WasModifiedAfterRecording = false;
}
