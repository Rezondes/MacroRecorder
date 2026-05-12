using System.Windows;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Views.Editor;

public partial class InsertMouseClickDialog : Window
{
    private readonly ICursorPositionProvider _cursor;

    public InsertMouseClickDialog(ICursorPositionProvider cursor)
    {
        _cursor = cursor;
        InitializeComponent();
        ButtonCombo.ItemsSource = Enum.GetValues<MouseButtonKind>();
        ButtonCombo.SelectedIndex = 0;
    }

    public MouseButtonKind SelectedButton =>
        ButtonCombo.SelectedItem is MouseButtonKind selectedKind ? selectedKind : MouseButtonKind.Left;

    public int ScreenX => int.TryParse(XBox.Text, out var parsedScreenX) ? parsedScreenX : 0;

    public int ScreenY => int.TryParse(YBox.Text, out var parsedScreenY) ? parsedScreenY : 0;

    private void OnCaptureClick(object sender, RoutedEventArgs routedEventArgs)
    {
        var (capturedScreenX, capturedScreenY) = _cursor.GetScreenPosition();
        XBox.Text = capturedScreenX.ToString();
        YBox.Text = capturedScreenY.ToString();
    }

    private void OnOk(object sender, RoutedEventArgs routedEventArgs)
    {
        DialogResult = true;
        Close();
    }
}
