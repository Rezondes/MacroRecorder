using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Views.Editor;

public partial class InsertMouseClickView : UserControl, IContentModalEscape
{
    private readonly Action<bool> _onCompleted;

    public InsertMouseClickView(Action<bool> onCompleted)
    {
        _onCompleted = onCompleted;
        InitializeComponent();
        ButtonCombo.ItemsSource = Enum.GetValues<MouseButtonKind>();
        ButtonCombo.SelectedIndex = 0;
    }

    public MouseButtonKind SelectedButton =>
        ButtonCombo.SelectedItem is MouseButtonKind selectedKind ? selectedKind : MouseButtonKind.Left;

    public int ScreenX => int.TryParse(XBox.Text, out var parsedScreenX) ? parsedScreenX : 0;

    public int ScreenY => int.TryParse(YBox.Text, out var parsedScreenY) ? parsedScreenY : 0;

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnOkClick(object sender, RoutedEventArgs e) => _onCompleted(true);
}
