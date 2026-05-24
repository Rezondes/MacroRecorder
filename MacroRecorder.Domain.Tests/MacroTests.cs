namespace MacroRecorder.Domain.Tests;

public sealed class MacroTests
{
    [Fact]
    public void CreateEmpty_rejects_blank_name()
    {
        Assert.Throws<ArgumentException>(() => Macro.CreateEmpty("  "));
    }

    [Fact]
    public void Rename_updates_name_and_bumps_document_version()
    {
        var macro = Macro.CreateEmpty("sample");
        var versionBefore = macro.DocumentVersion;

        macro.Rename("renamed");

        Assert.Equal("renamed", macro.Name);
        Assert.NotEqual(versionBefore, macro.DocumentVersion);
        Assert.True(macro.WasModifiedAfterRecording);
    }

    [Fact]
    public void AssignNameOnly_updates_name_without_bumping_document_version()
    {
        var macro = Macro.CreateEmpty("sample");
        var versionBefore = macro.DocumentVersion;

        macro.AssignNameOnly("renamed");

        Assert.Equal("renamed", macro.Name);
        Assert.Equal(versionBefore, macro.DocumentVersion);
        Assert.True(macro.WasModifiedAfterRecording);
    }

    [Fact]
    public void ApplyRecordingMerge_replaces_events_and_metadata()
    {
        var macro = Macro.CreateEmpty("sample");
        var versionBefore = macro.DocumentVersion;
        var metadata = RecordingMetadata.ForNewSession(null);
        var events = new List<RecordedInputEvent>
        {
            new KeyDownRecordedEvent
            {
                DelayBefore = TimeSpan.FromMilliseconds(10),
                Sequence = 1,
                Vk = 0x41,
                ScanCode = 0,
                IsExtendedKey = false,
                IsAltDown = false,
                IsInjected = false
            }
        };

        macro.ApplyRecordingMerge(events, metadata);

        Assert.Single(macro.Events);
        Assert.Same(metadata, macro.Metadata);
        Assert.NotEqual(versionBefore, macro.DocumentVersion);
    }

    [Fact]
    public void RemoveEventAt_bumps_document_version()
    {
        var macro = Macro.CreateEmpty("sample");
        macro.AppendEvent(new KeyDownRecordedEvent
        {
            DelayBefore = TimeSpan.Zero,
            Sequence = 1,
            Vk = 0x41,
            ScanCode = 0,
            IsExtendedKey = false,
            IsAltDown = false,
            IsInjected = false
        });
        var versionBefore = macro.DocumentVersion;

        macro.RemoveEventAt(0);

        Assert.Empty(macro.Events);
        Assert.NotEqual(versionBefore, macro.DocumentVersion);
    }
}
