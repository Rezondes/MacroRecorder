using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroRecorder.Application.Ports;
using MacroRecorder.App.Services;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Views.Editor;

public partial class PromptPlaybackChordView : UserControl, IContentModalEscape
{
    private readonly IUiLocalizer _loc;
    private readonly Action<bool> _onCompleted;
    private readonly IReadOnlyList<PlaybackKeyChord> _blocked;

    public PromptPlaybackChordView(
        IUiLocalizer loc,
        string title,
        string message,
        IReadOnlyList<PlaybackKeyChord> blockedChords,
        Action<bool> onCompleted)
    {
        _loc = loc;
        _onCompleted = onCompleted;
        _blocked = blockedChords;
        InitializeComponent();
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        HintBlock.Text = loc.GetString("Hotkey_Capture_Hint");
        Loaded += (_, _) =>
        {
            Focus();
            Keyboard.Focus(this);
        };
    }

    public PlaybackKeyChord? CapturedChord { get; private set; }

    public void CancelFromHost() => CloseModal(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => CloseModal(false);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseModal(false);
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierOnlyKey(key))
        {
            e.Handled = true;
            return;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
            return;

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        var win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

        var chord = new PlaybackKeyChord(ctrl, alt, shift, win, vk);
        if (!chord.HasNonModifierKey)
            return;

        foreach (var blocked in _blocked)
        {
            if (blocked.Ctrl == chord.Ctrl && blocked.Alt == chord.Alt && blocked.Shift == chord.Shift &&
                blocked.Win == chord.Win && blocked.VirtualKey == chord.VirtualKey)
            {
                ErrorBlock.Text = _loc.GetString("Hotkey_Error_AlreadyAssigned");
                ErrorBlock.Visibility = Visibility.Visible;
                e.Handled = true;
                return;
            }
        }

        ErrorBlock.Visibility = Visibility.Collapsed;
        CapturedChord = chord;
        CloseModal(true);
        e.Handled = true;
    }

    private void CloseModal(bool confirmed)
    {
        if (!confirmed)
            CapturedChord = null;
        _onCompleted(confirmed);
    }

    private static bool IsModifierOnlyKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System;
}
