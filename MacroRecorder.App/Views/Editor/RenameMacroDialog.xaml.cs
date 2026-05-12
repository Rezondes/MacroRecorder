using System.Windows;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Views.Editor;

public partial class RenameMacroDialog : Window
{
    private readonly IUiLocalizer _loc;

    public RenameMacroDialog(IUiLocalizer loc, string currentName)
    {
        _loc = loc;
        InitializeComponent();
        NameBox.Text = currentName;
    }

    public string NewName => NameBox.Text.Trim();

    private void OnOk(object sender, RoutedEventArgs routedEventArgs)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, _loc.GetString("DialogRename_EnterName"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
