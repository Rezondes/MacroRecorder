using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Views;

public partial class QueueCreatorView
{
    public const string MacroIdDragFormat = "MacroRecorder.MacroId";

    private Point _macroDragStart;
    private bool _macroDragMouseDown;

    public QueueCreatorView()
    {
        InitializeComponent();
    }

    private void OnMacroListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _macroDragMouseDown = true;
        _macroDragStart = e.GetPosition(null);
    }

    private void OnMacroListPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _macroDragMouseDown = false;
    }

    private void OnMacroListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_macroDragMouseDown || e.LeftButton != MouseButtonState.Pressed)
            return;
        var current = e.GetPosition(null);
        if ((current - _macroDragStart).LengthSquared < 36)
            return;
        if (MacroListView.SelectedItem is not MacroSummary summary)
            return;
        _macroDragMouseDown = false;
        try
        {
            DragDrop.DoDragDrop(MacroListView, new DataObject(MacroIdDragFormat, summary.Id.Value), DragDropEffects.Copy);
        }
        catch
        {
            // ignore drag failures
        }
    }

    private static bool TryGetMacroIdFromDataObject(IDataObject? data, out MacroId macroId)
    {
        macroId = default;
        if (data is null)
            return false;
        if (!data.GetDataPresent(MacroIdDragFormat))
            return false;
        var payload = data.GetData(MacroIdDragFormat);
        if (payload is not string value || string.IsNullOrWhiteSpace(value))
            return false;
        try
        {
            macroId = MacroId.Parse(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void OnStepsAreaDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetMacroIdFromDataObject(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnStepsAreaDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not QueueCreatorViewModel viewModel)
            return;
        if (!TryGetMacroIdFromDataObject(e.Data, out var macroId))
            return;
        viewModel.AddStepForMacro(macroId);
        e.Handled = true;
    }
}
