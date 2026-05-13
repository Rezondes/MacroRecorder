using System.Windows;

namespace MacroRecorder.Wpf.Controls;

/// <summary>
/// Attached properties for MDL2 icon chrome on <see cref="System.Windows.Controls.Button"/>.
/// Tooltips use the shared <see cref="UiToolTip"/> styling (implicit <c>ToolTip</c> style in AppControls).
/// </summary>
public static class IconButton
{
    public static readonly DependencyProperty IconGlyphProperty = DependencyProperty.RegisterAttached(
        "IconGlyph",
        typeof(string),
        typeof(IconButton),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ShowIconProperty = DependencyProperty.RegisterAttached(
        "ShowIcon",
        typeof(bool),
        typeof(IconButton),
        new PropertyMetadata(true));

    public static string? GetIconGlyph(DependencyObject obj) => (string?)obj.GetValue(IconGlyphProperty);

    public static void SetIconGlyph(DependencyObject obj, string? value) => obj.SetValue(IconGlyphProperty, value);

    public static bool GetShowIcon(DependencyObject obj) => (bool)obj.GetValue(ShowIconProperty);

    public static void SetShowIcon(DependencyObject obj, bool value) => obj.SetValue(ShowIconProperty, value);
}
