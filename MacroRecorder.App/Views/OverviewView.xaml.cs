using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroRecorder.App.Services;

namespace MacroRecorder.App.Views;

public partial class OverviewView
{
    public OverviewView() => InitializeComponent();

    private void OnOverviewPlayPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
            PlaybackCursorRestoreSession.ArmFromButton(button);
    }
}
