using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Views;

public partial class UpdateAvailableView : UserControl, IContentModalEscape
{
    private readonly Action<bool> _complete;
    private readonly IUiLocalizer _loc;
    private readonly UpdateCheckResult _result;

    public UpdateAvailableView(
        IUiLocalizer loc,
        UpdateCheckResult result,
        Action<bool> complete)
    {
        _loc = loc;
        _result = result;
        _complete = complete;
        InitializeComponent();
        TitleBlock.Text = _loc.GetString("Update_AvailableTitle");
        MessageBlock.Text = string.Format(
            _loc.CurrentUiCulture,
            _loc.GetString("Update_AvailableBody"),
            _result.LatestVersion,
            _result.CurrentVersion);
    }

    public void CancelFromHost() => _complete(false);

    private void OnLaterClick(object sender, RoutedEventArgs e) => _complete(false);

    private void OnOpenReleaseClick(object sender, RoutedEventArgs e) => _complete(true);
}
