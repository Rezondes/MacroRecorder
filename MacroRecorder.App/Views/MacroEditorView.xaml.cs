using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MacroRecorder.App.ViewModels;

namespace MacroRecorder.App.Views;

public partial class MacroEditorView : UserControl
{
    private const string RowDragFormat = "MacroRecorder.MacroEditor.RowIndex";

    private Point _dragStart;
    private int? _dragSourceRowIndex;

    public MacroEditorView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MacroEditorViewModel editorViewModel)
            editorViewModel.RequestTimelineScrollToEnd -= OnScrollTimelineToEnd;
    }

    private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
    {
        if (DataContext is MacroEditorViewModel editorViewModel)
        {
            var owner = Window.GetWindow(this);
            if (owner is not null)
                editorViewModel.AttachOwner(owner);
            editorViewModel.RequestTimelineScrollToEnd += OnScrollTimelineToEnd;
        }
    }

    private void OnScrollTimelineToEnd()
    {
        Dispatcher.BeginInvoke(() =>
        {
            TimelineList.UpdateLayout();
            if (TimelineList.Items.Count > 0)
                TimelineList.ScrollIntoView(TimelineList.Items[^1]);
        }, DispatcherPriority.Loaded);
    }

    private void OnInsertStepMenuItemClick(object sender, RoutedEventArgs e) =>
        InsertStepMenuToggle.IsChecked = false;

    private void OnTimelineDoubleClick(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        if (DataContext is MacroEditorViewModel editorViewModel && editorViewModel.EditSelectedCommand.CanExecute(null))
            editorViewModel.EditSelectedCommand.Execute(null);
    }

    private void OnTimelinePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        if (DataContext is MacroEditorViewModel { IsRecording: true })
            return;
        _dragSourceRowIndex = null;
        if (sender is not ListView listView)
            return;
        if (FindAncestor<ListViewItem>(mouseButtonEventArgs.OriginalSource as DependencyObject) is not { } item)
            return;
        var rowIndexFromHitItem = listView.ItemContainerGenerator.IndexFromContainer(item);
        if (rowIndexFromHitItem < 0)
            return;
        _dragSourceRowIndex = rowIndexFromHitItem;
        _dragStart = mouseButtonEventArgs.GetPosition(null);
    }

    private void OnTimelinePreviewMouseMove(object sender, MouseEventArgs mouseEventArgs)
    {
        if (mouseEventArgs.LeftButton != MouseButtonState.Pressed || _dragSourceRowIndex is not int sourceRowIndex)
            return;
        if (sender is not ListView listView)
            return;
        var delta = mouseEventArgs.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var dragData = new DataObject(RowDragFormat, sourceRowIndex);
        try
        {
            DragDrop.DoDragDrop(listView, dragData, DragDropEffects.Move);
        }
        finally
        {
            _dragSourceRowIndex = null;
            ClearDropIndicator();
        }
    }

    private void OnTimelinePreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        _dragSourceRowIndex = null;
        ClearDropIndicator();
    }

    private void OnTimelineDragOver(object sender, DragEventArgs dragEventArgs)
    {
        if (sender is not ListView listView)
            return;

        if (!dragEventArgs.Data.GetDataPresent(RowDragFormat))
        {
            dragEventArgs.Effects = DragDropEffects.None;
            dragEventArgs.Handled = true;
            ClearDropIndicator();
            return;
        }

        dragEventArgs.Effects = DragDropEffects.Move;
        dragEventArgs.Handled = true;

        var pointerPositionInListView = dragEventArgs.GetPosition(listView);
        var insertBefore = GetInsertIndexBeforeRow(listView, pointerPositionInListView);
        UpdateDropIndicator(listView, insertBefore);
    }

    private void OnTimelineDrop(object sender, DragEventArgs dragEventArgs)
    {
        dragEventArgs.Handled = true;
        ClearDropIndicator();
        if (DataContext is not MacroEditorViewModel editorViewModel)
            return;
        if (sender is not ListView listView)
            return;
        if (!dragEventArgs.Data.GetDataPresent(RowDragFormat))
            return;
        var sourceRowIndex = (int)dragEventArgs.Data.GetData(RowDragFormat)!;
        var insertBefore = GetInsertIndexBeforeRow(listView, dragEventArgs.GetPosition(listView));
        editorViewModel.ReorderRowDrag(sourceRowIndex, insertBefore);
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
        while (child != null && child is not T)
            child = VisualTreeHelper.GetParent(child);
        return child as T;
    }
}
