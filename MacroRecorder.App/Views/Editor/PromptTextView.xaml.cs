using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Views.Editor;

public partial class PromptTextView : UserControl, IContentModalEscape
{
    private const int SpinnerMaxMs = int.MaxValue;

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
        InputBox.Text = defaultValue;
        InputBox.TextChanged += (_, _) => ErrorBlock.Visibility = Visibility.Collapsed;

        if (_restrictInputToDigits)
        {
            NumericSpinner.Visibility = Visibility.Visible;
            InputBox.PreviewTextInput += OnInputPreviewTextInput;
            InputBox.PreviewKeyDown += OnInputPreviewKeyDown;
            InputBox.AddHandler(DataObject.PastingEvent, new DataObjectPastingEventHandler(OnInputPasting), true);
        }

        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string ResultText => InputBox.Text;

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (_validator is not null)
        {
            var err = _validator(InputBox.Text);
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

    private void OnSpinnerUpClick(object sender, RoutedEventArgs e)
    {
        var n = ParseMillisOrZero(InputBox.Text);
        if (n < 1)
            n = 1;
        else if (n >= SpinnerMaxMs)
            return;
        else
            n++;

        InputBox.Text = n.ToString(CultureInfo.InvariantCulture);
        FocusInputCaretEnd();
    }

    private void OnSpinnerDownClick(object sender, RoutedEventArgs e)
    {
        var n = ParseMillisOrZero(InputBox.Text);
        if (n <= 1)
            InputBox.Text = "1";
        else
            InputBox.Text = (n - 1).ToString(CultureInfo.InvariantCulture);

        FocusInputCaretEnd();
    }

    private static int ParseMillisOrZero(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private void FocusInputCaretEnd()
    {
        InputBox.Focus();
        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private void OnInputPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!_restrictInputToDigits)
            return;
        if (e.Text is null || e.Text.Length == 0)
            return;
        if (e.Text.Any(static c => !char.IsDigit(c)))
            e.Handled = true;
    }

    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_restrictInputToDigits)
            return;
        if (e.Key == Key.Space)
            e.Handled = true;
    }

    private void OnInputPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!_restrictInputToDigits)
            return;
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string paste || paste.Length == 0)
        {
            e.CancelCommand();
            return;
        }

        if (paste.Any(static c => !char.IsDigit(c)))
            e.CancelCommand();
    }
}
