using System.Windows;
using System.Windows.Controls;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Services;

public sealed class WpfUserDialogService(IUiLocalizer loc) : IUserDialogService
{
    public string? PromptText(string title, string message, string defaultValue = "")
    {
        var promptWindow = new Window
        {
            Title = title,
            Width = 420,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current?.MainWindow,
            ResizeMode = ResizeMode.NoResize
        };

        var rootGrid = new Grid { Margin = new Thickness(12) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(messageBlock, 0);
        rootGrid.Children.Add(messageBlock);

        var inputTextBox = new TextBox { Text = defaultValue };
        Grid.SetRow(inputTextBox, 1);
        rootGrid.Children.Add(inputTextBox);

        var okButton = new Button
        {
            Content = loc.GetString("Common_Ok"),
            Width = 80,
            IsDefault = true,
            Margin = new Thickness(0, 8, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancelButton = new Button
        {
            Content = loc.GetString("Common_Cancel"),
            Width = 80,
            IsCancel = true,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(okButton);
        Grid.SetRow(buttonPanel, 2);
        rootGrid.Children.Add(buttonPanel);

        promptWindow.Content = rootGrid;
        string? result = null;
        okButton.Click += (_, _) =>
        {
            result = inputTextBox.Text;
            promptWindow.DialogResult = true;
            promptWindow.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            promptWindow.DialogResult = false;
            promptWindow.Close();
        };

        return promptWindow.ShowDialog() == true ? result : null;
    }

    public void ShowInfo(string message) =>
        MessageBox.Show(message, loc.GetString("Common_AppTitle"), MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string message) =>
        MessageBox.Show(message, loc.GetString("Common_AppTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) ==
        MessageBoxResult.Yes;

    public UnsavedChangesPromptResult PromptUnsavedChanges(string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            MinHeight = 140,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current?.MainWindow,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };

        UnsavedChangesPromptResult? picked = null;

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(messageBlock, 0);
        root.Children.Add(messageBlock);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var saveButton = new Button
        {
            Content = loc.GetString("Editor_Save"),
            MinWidth = 88,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true,
            Padding = new Thickness(12, 4, 12, 4)
        };
        var discardButton = new Button
        {
            Content = loc.GetString("Editor_UnsavedDiscard"),
            MinWidth = 88,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(12, 4, 12, 4)
        };
        var cancelButton = new Button
        {
            Content = loc.GetString("Common_Cancel"),
            MinWidth = 88,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true,
            Padding = new Thickness(12, 4, 12, 4)
        };

        saveButton.Click += (_, _) =>
        {
            picked = UnsavedChangesPromptResult.Save;
            dialog.Close();
        };
        discardButton.Click += (_, _) =>
        {
            picked = UnsavedChangesPromptResult.Discard;
            dialog.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            picked = UnsavedChangesPromptResult.Cancel;
            dialog.Close();
        };

        buttonRow.Children.Add(saveButton);
        buttonRow.Children.Add(discardButton);
        buttonRow.Children.Add(cancelButton);
        Grid.SetRow(buttonRow, 1);
        root.Children.Add(buttonRow);

        dialog.Content = root;
        dialog.ShowDialog();
        return picked ?? UnsavedChangesPromptResult.Cancel;
    }
}
