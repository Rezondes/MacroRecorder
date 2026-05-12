using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Views.Editor;

public partial class InsertMouseClickView : UserControl, IContentModalEscape
{
    private readonly ICursorPositionProvider _cursor;
    private readonly Action<bool> _onCompleted;

    public InsertMouseClickView(ICursorPositionProvider cursor, Action<bool> onCompleted)
    {
        _cursor = cursor;
        _onCompleted = onCompleted;
        InitializeComponent();
        ButtonCombo.ItemsSource = Enum.GetValues<MouseButtonKind>();
        ButtonCombo.SelectedIndex = 0;
        ThemeTextBox(XBox);
        ThemeTextBox(YBox);
    }

    private static void ThemeTextBox(TextBox box)
    {
        box.SetResourceReference(TextBox.BackgroundProperty, "UiBrush.SurfaceElevated");
        box.SetResourceReference(TextBox.ForegroundProperty, "UiBrush.TextPrimary");
        box.SetResourceReference(TextBox.BorderBrushProperty, "UiBrush.Border");
        box.BorderThickness = new Thickness(1);
    }

    public MouseButtonKind SelectedButton =>
        ButtonCombo.SelectedItem is MouseButtonKind selectedKind ? selectedKind : MouseButtonKind.Left;

    public int ScreenX => int.TryParse(XBox.Text, out var parsedScreenX) ? parsedScreenX : 0;

    public int ScreenY => int.TryParse(YBox.Text, out var parsedScreenY) ? parsedScreenY : 0;

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        var (capturedScreenX, capturedScreenY) = _cursor.GetScreenPosition();
        XBox.Text = capturedScreenX.ToString();
        YBox.Text = capturedScreenY.ToString();
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => _onCompleted(true);
}
