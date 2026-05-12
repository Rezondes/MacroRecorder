using System.Windows;
using MacroRecorder.App.ViewModels;

namespace MacroRecorder.App;

public partial class RecordWindow : Window
{
    public RecordWindow(RecordViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
