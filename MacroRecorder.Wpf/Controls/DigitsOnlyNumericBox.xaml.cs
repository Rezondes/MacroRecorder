using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MacroRecorder.Wpf.Controls;

public partial class DigitsOnlyNumericBox : UserControl
{
    private bool _textXferGuard;

    public DigitsOnlyNumericBox()
    {
        InitializeComponent();
        PART_TextBox.PreviewTextInput += PART_TextBox_OnPreviewTextInput;
        PART_TextBox.PreviewKeyDown += PART_TextBox_OnPreviewKeyDown;
        DataObject.AddPastingHandler(PART_TextBox, PART_TextBox_OnPasting);
        PART_TextBox.TextChanged += PART_TextBox_OnTextChanged;
        Loaded += (_, _) =>
        {
            PART_TextBox.FontSize = InputFontSize;
            ChromeBorder.CornerRadius = CornerRadius;
            RefreshSpinnerUi();
        };
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(DigitsOnlyNumericBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTextPropertyChanged));

    public static readonly DependencyProperty DigitsOnlyProperty = DependencyProperty.Register(
        nameof(DigitsOnly),
        typeof(bool),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(true, (d, _) => ((DigitsOnlyNumericBox)d).RefreshSpinnerUi()));

    public static readonly DependencyProperty ShowSpinnerProperty = DependencyProperty.Register(
        nameof(ShowSpinner),
        typeof(bool),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(false, (d, _) => ((DigitsOnlyNumericBox)d).RefreshSpinnerUi()));

    public static readonly DependencyProperty SpinnerStepProperty = DependencyProperty.Register(
        nameof(SpinnerStep),
        typeof(int),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(1));

    public static readonly DependencyProperty MinimumValueProperty = DependencyProperty.Register(
        nameof(MinimumValue),
        typeof(int),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(int.MinValue));

    public static readonly DependencyProperty MaximumValueProperty = DependencyProperty.Register(
        nameof(MaximumValue),
        typeof(int),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(int.MaxValue));

    public static readonly DependencyProperty SpinnerDelayProperty = DependencyProperty.Register(
        nameof(SpinnerDelay),
        typeof(int),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(400, OnSpinnerTimingChanged));

    public static readonly DependencyProperty SpinnerIntervalProperty = DependencyProperty.Register(
        nameof(SpinnerInterval),
        typeof(int),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(80, OnSpinnerTimingChanged));

    public static readonly DependencyProperty SpinUpToolTipProperty = DependencyProperty.Register(
        nameof(SpinUpToolTip),
        typeof(object),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(null, (d, _) => ((DigitsOnlyNumericBox)d).ApplySpinToolTips()));

    public static readonly DependencyProperty SpinDownToolTipProperty = DependencyProperty.Register(
        nameof(SpinDownToolTip),
        typeof(object),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(null, (d, _) => ((DigitsOnlyNumericBox)d).ApplySpinToolTips()));

    public static readonly DependencyProperty InputFontSizeProperty = DependencyProperty.Register(
        nameof(InputFontSize),
        typeof(double),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(13.0, OnInputFontSizeChanged));

    public static readonly DependencyProperty MinInnerHeightProperty = DependencyProperty.Register(
        nameof(MinInnerHeight),
        typeof(double),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(40.0));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(DigitsOnlyNumericBox),
        new PropertyMetadata(new CornerRadius(8), OnCornerRadiusChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool DigitsOnly
    {
        get => (bool)GetValue(DigitsOnlyProperty);
        set => SetValue(DigitsOnlyProperty, value);
    }

    public bool ShowSpinner
    {
        get => (bool)GetValue(ShowSpinnerProperty);
        set => SetValue(ShowSpinnerProperty, value);
    }

    public int SpinnerStep
    {
        get => (int)GetValue(SpinnerStepProperty);
        set => SetValue(SpinnerStepProperty, value);
    }

    public int MinimumValue
    {
        get => (int)GetValue(MinimumValueProperty);
        set => SetValue(MinimumValueProperty, value);
    }

    public int MaximumValue
    {
        get => (int)GetValue(MaximumValueProperty);
        set => SetValue(MaximumValueProperty, value);
    }

    public int SpinnerDelay
    {
        get => (int)GetValue(SpinnerDelayProperty);
        set => SetValue(SpinnerDelayProperty, value);
    }

    public int SpinnerInterval
    {
        get => (int)GetValue(SpinnerIntervalProperty);
        set => SetValue(SpinnerIntervalProperty, value);
    }

    public object? SpinUpToolTip
    {
        get => GetValue(SpinUpToolTipProperty);
        set => SetValue(SpinUpToolTipProperty, value);
    }

    public object? SpinDownToolTip
    {
        get => GetValue(SpinDownToolTipProperty);
        set => SetValue(SpinDownToolTipProperty, value);
    }

    public double InputFontSize
    {
        get => (double)GetValue(InputFontSizeProperty);
        set => SetValue(InputFontSizeProperty, value);
    }

    public double MinInnerHeight
    {
        get => (double)GetValue(MinInnerHeightProperty);
        set => SetValue(MinInnerHeightProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public event TextChangedEventHandler? TextChanged;

    public void SelectAll()
    {
        PART_TextBox.Focus();
        PART_TextBox.SelectAll();
    }

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DigitsOnlyNumericBox c)
            return;
        if (c._textXferGuard)
            return;
        c._textXferGuard = true;
        try
        {
            c.PART_TextBox.Text = (string?)e.NewValue ?? string.Empty;
        }
        finally
        {
            c._textXferGuard = false;
        }
    }

    private static void OnInputFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DigitsOnlyNumericBox c || !c.IsLoaded)
            return;
        c.PART_TextBox.FontSize = (double)e.NewValue;
    }

    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DigitsOnlyNumericBox c || !c.IsLoaded)
            return;
        c.ChromeBorder.CornerRadius = (CornerRadius)e.NewValue;
    }

    private static void OnSpinnerTimingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DigitsOnlyNumericBox c)
            return;
        if (!c.IsLoaded)
            return;
        c.SpinnerUpButton.Delay = c.SpinnerDelay;
        c.SpinnerUpButton.Interval = c.SpinnerInterval;
        c.SpinnerDownButton.Delay = c.SpinnerDelay;
        c.SpinnerDownButton.Interval = c.SpinnerInterval;
    }

    private void PART_TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_textXferGuard)
            return;
        _textXferGuard = true;
        try
        {
            SetCurrentValue(TextProperty, PART_TextBox.Text);
            TextChanged?.Invoke(this, e);
        }
        finally
        {
            _textXferGuard = false;
        }
    }

    private void PART_TextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!DigitsOnly)
            return;
        if (!IsAllDigits(e.Text))
            e.Handled = true;
    }

    private void PART_TextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DigitsOnly && ShowSpinner)
        {
            if (e.Key == Key.Up)
            {
                Bump(+1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                Bump(-1);
                e.Handled = true;
                return;
            }
        }

        if (!DigitsOnly)
            return;
        if (e.Key == Key.Space)
            e.Handled = true;
    }

    private void PART_TextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!DigitsOnly)
            return;
        var text = e.DataObject.GetData(DataFormats.UnicodeText) as string
            ?? e.DataObject.GetData(DataFormats.Text) as string
            ?? string.Empty;
        if (text.Length == 0 && !e.DataObject.GetDataPresent(DataFormats.Text) &&
            !e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        if (!IsAllDigits(text))
            e.CancelCommand();
    }

    private static bool IsAllDigits(string text)
    {
        foreach (var ch in text)
        {
            if (ch is < '0' or > '9')
                return false;
        }
        return true;
    }

    private void RefreshSpinnerUi()
    {
        var show = ShowSpinner && DigitsOnly;
        SpinnerPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded)
        {
            SpinnerUpButton.Delay = SpinnerDelay;
            SpinnerUpButton.Interval = SpinnerInterval;
            SpinnerDownButton.Delay = SpinnerDelay;
            SpinnerDownButton.Interval = SpinnerInterval;
        }
        ApplySpinToolTips();
    }

    private void ApplySpinToolTips()
    {
        ToolTipService.SetToolTip(SpinnerUpButton, SpinUpToolTip);
        ToolTipService.SetToolTip(SpinnerDownButton, SpinDownToolTip);
    }

    private void SpinnerUpButton_OnClick(object sender, RoutedEventArgs e) => Bump(+1);

    private void SpinnerDownButton_OnClick(object sender, RoutedEventArgs e) => Bump(-1);

    private void Bump(int sign)
    {
        if (!DigitsOnly || !ShowSpinner)
            return;
        var step = SpinnerStep;
        if (step <= 0)
            step = 1;
        if (!int.TryParse(PART_TextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            n = 0;
        long v = n + (long)sign * step;
        if (v < MinimumValue)
            v = MinimumValue;
        if (v > MaximumValue)
            v = MaximumValue;
        Text = v.ToString(CultureInfo.InvariantCulture);
        PART_TextBox.SelectAll();
    }
}
