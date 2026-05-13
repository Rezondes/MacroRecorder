using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroRecorder.App.Controls;
using MacroRecorder.App.ViewModels;

namespace MacroRecorder.App.Views;

public partial class OverviewView
{
    public const string OverviewReorderDragFormat = "MacroRecorder.OverviewReorderIndex";

    private Point _dragReorderStart;
    private bool _dragReorderMouseDown;
    private int _dragReorderSourceIndex = -1;

    public OverviewView()
    {
        InitializeComponent();
        Unloaded += (_, _) => ClearDropIndicator();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragReorderMouseDown = true;
        _dragReorderStart = e.GetPosition(null);
        _dragReorderSourceIndex = TryGetMacroRowIndexExcludingChrome(e);
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragReorderMouseDown = false;
        ClearDropIndicator();
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragReorderMouseDown || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (_dragReorderSourceIndex < 0)
            return;
        var current = e.GetPosition(null);
        var delta = current - _dragReorderStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        _dragReorderMouseDown = false;
        try
        {
            DragDrop.DoDragDrop(
                MacroOverviewList,
                new DataObject(OverviewReorderDragFormat, _dragReorderSourceIndex),
                DragDropEffects.Move);
        }
        catch
        {
            // ignore drag failures
        }
        finally
        {
            _dragReorderSourceIndex = -1;
            ClearDropIndicator();
        }
    }

    private void OnMacroListDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ListView listView)
            return;

        if (!e.Data.GetDataPresent(OverviewReorderDragFormat) ||
            e.Data.GetData(OverviewReorderDragFormat) is not int sourceIndex ||
            DataContext is not MainViewModel viewModel)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            ClearDropIndicator();
            return;
        }

        if (sourceIndex < 0 || sourceIndex >= viewModel.Macros.Count)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            ClearDropIndicator();
            return;
        }

        var insertBefore = GetInsertIndexBeforeRow(listView, e.GetPosition(listView));
        if (DropIsNoOpForSingleRow(sourceIndex, insertBefore))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            ClearDropIndicator();
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        UpdateDropIndicator(listView, insertBefore);
    }

    private void OnMacroListDragLeave(object sender, DragEventArgs e)
    {
        if (sender is not ListView listView)
            return;
        if (!e.Data.GetDataPresent(OverviewReorderDragFormat))
            return;
        var position = e.GetPosition(listView);
        if (position.X < 0 || position.Y < 0 || position.X > listView.ActualWidth || position.Y > listView.ActualHeight)
            ClearDropIndicator();
    }

    private async void OnMacroListDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        ClearDropIndicator();
        if (DataContext is not MainViewModel viewModel)
            return;
        if (sender is not ListView listView)
            return;
        if (!e.Data.GetDataPresent(OverviewReorderDragFormat))
            return;
        if (e.Data.GetData(OverviewReorderDragFormat) is not int sourceIndex)
            return;
        if (sourceIndex < 0 || sourceIndex >= viewModel.Macros.Count)
            return;

        var insertBefore = GetInsertIndexBeforeRow(listView, e.GetPosition(listView));
        if (DropIsNoOpForSingleRow(sourceIndex, insertBefore))
            return;

        var count = viewModel.Macros.Count;
        var moveTarget = InsertBeforeIndexToMoveTargetIndex(sourceIndex, insertBefore, count);
        if (moveTarget != sourceIndex)
            await viewModel.ApplyMacroReorderAsync(sourceIndex, moveTarget).ConfigureAwait(true);
    }

    /// <summary>Same rule as editor timeline: dropping inside the dragged row (or directly adjacent without moving) is a no-op.</summary>
    private static bool DropIsNoOpForSingleRow(int sourceIndex, int insertBefore) =>
        insertBefore >= sourceIndex && insertBefore <= sourceIndex + 1;

    /// <summary>Maps "insert before row index" (0..Count) to <see cref="ObservableCollection{T}.Move"/> destination index.</summary>
    private static int InsertBeforeIndexToMoveTargetIndex(int sourceIndex, int insertBefore, int count)
    {
        insertBefore = Math.Clamp(insertBefore, 0, count);
        if (insertBefore > sourceIndex)
            return insertBefore - 1;
        return insertBefore;
    }

    private void ClearDropIndicator()
    {
        if (DropInsertIndicator is null)
            return;
        DropInsertIndicator.Visibility = Visibility.Collapsed;
    }

    private void UpdateDropIndicator(ListView listView, int insertBefore)
    {
        if (DropInsertIndicator is null || DropIndicatorCanvas is null)
            return;

        var canvasHeight = DropIndicatorCanvas.ActualHeight;
        var canvasWidth = DropIndicatorCanvas.ActualWidth;
        if (canvasWidth <= 0 || canvasHeight <= 0)
            return;

        if (listView.Items.Count == 0)
        {
            PositionIndicatorLine(0, canvasWidth);
            return;
        }

        if (insertBefore <= 0)
        {
            if (listView.ItemContainerGenerator.ContainerFromIndex(0) is not ListViewItem first)
            {
                ClearDropIndicator();
                return;
            }

            var firstRowTopY = first.TransformToVisual(DropIndicatorCanvas).Transform(new Point(0, 0)).Y;
            PositionIndicatorLine(ClampLineTop(firstRowTopY, canvasHeight), canvasWidth);
            return;
        }

        if (insertBefore >= listView.Items.Count)
        {
            var lastRowIndex = listView.Items.Count - 1;
            if (listView.ItemContainerGenerator.ContainerFromIndex(lastRowIndex) is not ListViewItem last)
            {
                ClearDropIndicator();
                return;
            }

            var lastRowBottomY = last.TransformToVisual(DropIndicatorCanvas).Transform(new Point(0, last.ActualHeight)).Y;
            PositionIndicatorLine(ClampLineTop(lastRowBottomY - DropInsertIndicator.Height, canvasHeight), canvasWidth);
            return;
        }

        if (listView.ItemContainerGenerator.ContainerFromIndex(insertBefore) is not ListViewItem item)
        {
            ClearDropIndicator();
            return;
        }

        var targetRowTopY = item.TransformToVisual(DropIndicatorCanvas).Transform(new Point(0, 0)).Y;
        PositionIndicatorLine(ClampLineTop(targetRowTopY - 1, canvasHeight), canvasWidth);
    }

    private void PositionIndicatorLine(double top, double width)
    {
        Canvas.SetLeft(DropInsertIndicator, 0);
        Canvas.SetTop(DropInsertIndicator, top);
        DropInsertIndicator.Width = Math.Max(0, width);
        DropInsertIndicator.Visibility = Visibility.Visible;
    }

    private static double ClampLineTop(double candidateTopY, double canvasHeight)
    {
        const double indicatorLineHeight = 3;
        return Math.Max(0, Math.Min(candidateTopY, Math.Max(0, canvasHeight - indicatorLineHeight)));
    }

    private static int GetInsertIndexBeforeRow(ListView listView, Point positionRelativeToListView)
    {
        var hitTestTarget = listView.InputHitTest(positionRelativeToListView);
        var row = FindAncestor<ListViewItem>(hitTestTarget as DependencyObject);
        if (row is null)
            return listView.Items.Count;

        var hitRowIndex = listView.ItemContainerGenerator.IndexFromContainer(row);
        if (hitRowIndex < 0)
            return listView.Items.Count;

        var rowTop = row.TransformToVisual(listView).Transform(new Point(0, 0));
        var relativeYWithinRow = positionRelativeToListView.Y - rowTop.Y;
        var half = Math.Max(4, row.ActualHeight / 2);
        return relativeYWithinRow < half ? hitRowIndex : hitRowIndex + 1;
    }

    private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null && child is not T)
            child = VisualTreeHelper.GetParent(child);
        return child as T;
    }

    private int TryGetMacroRowIndexExcludingChrome(MouseButtonEventArgs e)
    {
        var position = e.GetPosition(MacroOverviewList);
        var hit = VisualTreeHelper.HitTest(MacroOverviewList, position)?.VisualHit;
        var row = FindAncestorListViewItem(hit);
        if (row is null)
            return -1;
        if (IsClickOnRowInteractiveChrome(hit, row))
            return -1;
        return MacroOverviewList.ItemContainerGenerator.IndexFromContainer(row);
    }

    private static ListViewItem? FindAncestorListViewItem(DependencyObject? leaf)
    {
        for (var d = leaf; d != null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ListViewItem item)
                return item;
        }

        return null;
    }

    private static bool IsClickOnRowInteractiveChrome(DependencyObject? leaf, ListViewItem row)
    {
        for (var d = leaf; d is not null && !ReferenceEquals(d, row); d = VisualTreeHelper.GetParent(d))
        {
            if (d is Button or System.Windows.Controls.Primitives.RepeatButton or PlayMacroGlyphButton)
                return true;
        }

        return false;
    }
}
