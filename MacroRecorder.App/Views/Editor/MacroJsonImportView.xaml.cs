using System.IO;
using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using Microsoft.Win32;

namespace MacroRecorder.App.Views.Editor;

public partial class MacroJsonImportView : UserControl, IContentModalEscape
{
    private readonly IUiLocalizer _loc;
    private readonly IUserDialogService _dialogs;
    private readonly Func<string, Task<bool>> _importJsonAsync;
    private readonly Action<bool> _onCompleted;

    public MacroJsonImportView(
        IUiLocalizer loc,
        IUserDialogService dialogs,
        Func<string, Task<bool>> importJsonAsync,
        Action<bool> onCompleted)
    {
        _loc = loc;
        _dialogs = dialogs;
        _importJsonAsync = importJsonAsync;
        _onCompleted = onCompleted;
        InitializeComponent();
        JsonBox.SetResourceReference(ForegroundProperty, "UiBrush.TextPrimary");
        Loaded += (_, _) => JsonBox.Focus();
    }

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = _loc.GetString("Main_Import_FileFilter"),
            CheckFileExists = true
        };
        var owner = System.Windows.Application.Current?.MainWindow;
        var showResult = owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        if (showResult != true || string.IsNullOrWhiteSpace(dialog.FileName))
            return;

        try
        {
            JsonBox.Text = File.ReadAllText(dialog.FileName);
            JsonBox.CaretIndex = JsonBox.Text.Length;
            JsonBox.Focus();
        }
        catch (Exception exception)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Import_ErrorLoad", exception.Message));
        }
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await _importJsonAsync(JsonBox.Text).ConfigureAwait(true))
                _onCompleted(true);
        }
        catch (Exception exception)
        {
            _dialogs.ShowInfo(_loc.GetString("Main_Import_ErrorLoad", exception.Message));
        }
    }
}
