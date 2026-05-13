using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroRecorder.App.Services;

namespace MacroRecorder.App.Controls;

public partial class PlayMacroGlyphButton
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(PlayMacroGlyphButton));

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(PlayMacroGlyphButton));

    public PlayMacroGlyphButton() => InitializeComponent();

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private void OnPlayPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
            PlaybackCursorRestoreSession.ArmFromButton(button);
    }
}
