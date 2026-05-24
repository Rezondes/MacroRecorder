namespace MacroRecorder.Domain.Tests;

public sealed class PlaybackKeyChordTests
{
    [Fact]
    public void HasNonModifierKey_is_false_when_virtual_key_is_zero()
    {
        var chord = new PlaybackKeyChord(Ctrl: true, Alt: false, Shift: false, Win: false, VirtualKey: 0);

        Assert.False(chord.HasNonModifierKey);
    }

    [Fact]
    public void HasNonModifierKey_is_true_when_virtual_key_is_non_zero()
    {
        var chord = new PlaybackKeyChord(Ctrl: true, Alt: false, Shift: false, Win: false, VirtualKey: 0x50);

        Assert.True(chord.HasNonModifierKey);
    }

    [Fact]
    public void Record_equality_compares_all_fields()
    {
        var left = new PlaybackKeyChord(true, false, true, false, 0x50);
        var right = new PlaybackKeyChord(true, false, true, false, 0x50);
        var different = new PlaybackKeyChord(true, false, true, false, 0x51);

        Assert.Equal(left, right);
        Assert.NotEqual(left, different);
    }
}
