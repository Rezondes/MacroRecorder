using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Views.Editor;

public partial class PromptTextView : UserControl, IContentModalEscape
{
    private readonly Action<bool> _onCompleted;
    private readonly PromptTextValidator? _validator;
    private readonly bool _restrictInputToDigits;

    public PromptTextView(
        string title,
        string message,
        string defaultValue,
        Action<bool> onCompleted,
        PromptTextValidator? validator = null,
        bool restrictInputToDigits = false)
    {
        _onCompleted = onCompleted;
        _validator = validator;
        _restrictInputToDigits = restrictInputToDigits;
        InitializeComponent();
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        InputNumeric.Text = defaultValue;
        InputNumeric.DigitsOnly = restrictInputToDigits;
        InputNumeric.ShowSpinner = restrictInputToDigits;
        if (restrictInputToDigits)
        {
            InputNumeric.MinimumValue = 1;
            InputNumeric.MaximumValue = int.MaxValue;
            InputNumeric.SpinnerStep = 1;
        }

        InputNumeric.TextChanged += (_, _) => ErrorBlock.Visibility = Visibility.Collapsed;

        Loaded += (_, _) => InputNumeric.SelectAll();
    }

    public string ResultText => InputNumeric.Text;

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (_validator is not null)
        {
            var err = _validator(InputNumeric.Text);
            if (!string.IsNullOrEmpty(err))
            {
                ErrorBlock.Text = err;
                ErrorBlock.Visibility = Visibility.Visible;
                return;
            }
        }

        ErrorBlock.Visibility = Visibility.Collapsed;
        _onCompleted(true);
    }
}
