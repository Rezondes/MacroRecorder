using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.ViewModels;

namespace MacroRecorder.App.Views;

public partial class SettingsView
{
    private SettingsViewModel? _settingsVm;

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        _settingsVm = e.NewValue as SettingsViewModel;

    private void OnUnloaded(object sender, RoutedEventArgs e) => _settingsVm = null;

    private void SettingsTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tc || _settingsVm is null)
            return;

        // Ignore bubbled SelectionChanged from selectors inside tab content (e.g. list boxes).
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not TabItem)
            return;

        var idx = tc.SelectedIndex;
        if (idx == _settingsVm.SelectedSettingsTabIndex)
            return;

        if (!_settingsVm.TryChangeSettingsTab(idx))
            tc.SelectedIndex = _settingsVm.SelectedSettingsTabIndex;
    }
}
