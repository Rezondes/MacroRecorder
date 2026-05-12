using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Views.Editor;

public partial class RenameMacroView : UserControl, IContentModalEscape
{
    private readonly IUiLocalizer _loc;
    private readonly Action<string> _showValidation;
    private readonly Action<bool> _onCompleted;

    public RenameMacroView(IUiLocalizer loc, string currentName, Action<string> showValidation, Action<bool> onCompleted)
    {
        _loc = loc;
        _showValidation = showValidation;
        _onCompleted = onCompleted;
        InitializeComponent();
        NameBox.Text = currentName;
        NameBox.SetResourceReference(BackgroundProperty, "UiBrush.SurfaceElevated");
        NameBox.SetResourceReference(ForegroundProperty, "UiBrush.TextPrimary");
        NameBox.SetResourceReference(BorderBrushProperty, "UiBrush.Border");
        NameBox.BorderThickness = new Thickness(1);
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string NewName => NameBox.Text.Trim();

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            _showValidation(_loc.GetString("DialogRename_EnterName"));
            return;
        }

        _onCompleted(true);
    }
}
