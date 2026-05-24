using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace MacroRecorder.App.Views.Editor;

public partial class MacroJsonShareView : UserControl, IContentModalEscape
{
    private readonly IUiLocalizer _loc;
    private readonly IUserDialogService _dialogs;
    private readonly string _macroName;
    private readonly Action<bool> _onCompleted;
    private readonly ILogger<MacroJsonShareView> _logger;

    public MacroJsonShareView(
        IUiLocalizer loc,
        IUserDialogService dialogs,
        string macroName,
        string json,
        Action<bool> onCompleted,
        ILogger<MacroJsonShareView> logger)
    {
        _loc = loc;
        _dialogs = dialogs;
        _macroName = macroName;
        _onCompleted = onCompleted;
        _logger = logger;
        InitializeComponent();
        JsonBox.Text = json;
        JsonBox.SetResourceReference(ForegroundProperty, "UiBrush.TextPrimary");
        Loaded += (_, _) =>
        {
            JsonBox.Focus();
            JsonBox.SelectAll();
        };
    }

    public void CancelFromHost() => _onCompleted(false);

    private void OnCloseClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = _loc.GetString("Main_Export_FileFilter"),
            FileName = SanitizeFileNameForExport(_macroName) + ".json",
            AddExtension = true,
            DefaultExt = ".json"
        };
        var owner = System.Windows.Application.Current?.MainWindow;
        var showResult = owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        if (showResult != true || string.IsNullOrWhiteSpace(dialog.FileName))
            return;

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, JsonBox.Text).ConfigureAwait(true);
            _dialogs.ShowInfo(_loc.GetString("Main_Export_Success", dialog.FileName));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Macro JSON export failed");
            _dialogs.ShowInfo(_loc.GetString("Main_Export_ErrorWrite", exception.Message));
        }
    }

    private static string SanitizeFileNameForExport(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var trimmed = new string(chars).Trim();
        return string.IsNullOrEmpty(trimmed) ? "macro" : trimmed;
    }
}
