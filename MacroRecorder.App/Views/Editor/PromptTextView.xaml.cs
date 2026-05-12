using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;

namespace MacroRecorder.App.Views.Editor;

public partial class PromptTextView : UserControl, IContentModalEscape
{
    private readonly Action<bool> _onCompleted;

    public PromptTextView(string title, string message, string defaultValue, Action<bool> onCompleted)
    {
        _onCompleted = onCompleted;
        InitializeComponent();
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        InputBox.Text = defaultValue;
        InputBox.SetResourceReference(BackgroundProperty, "UiBrush.SurfaceElevated");
        InputBox.SetResourceReference(ForegroundProperty, "UiBrush.TextPrimary");
        InputBox.SetResourceReference(BorderBrushProperty, "UiBrush.Border");
        InputBox.BorderThickness = new Thickness(1);
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string ResultText => InputBox.Text;

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnOkClick(object sender, RoutedEventArgs e) => _onCompleted(true);
}
