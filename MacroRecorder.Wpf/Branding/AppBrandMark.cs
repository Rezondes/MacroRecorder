using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacroRecorder.Wpf.Branding;

/// <summary>Vector app logo (rounded square + “MR”) for in-app branding and icon export.</summary>
public static class AppBrandMark
{
    public const double CanvasSize = 256;
    private const double Inset = 16;
    private const double CornerRadius = 48;

    public static DrawingImage DrawingImage { get; } = CreateDrawingImage();

    public static ImageSource BitmapSource(int pixelSize)
    {
        var drawing = DrawingImage.Drawing;
        var bounds = drawing?.Bounds ?? new Rect(0, 0, CanvasSize, CanvasSize);
        var scale = pixelSize / Math.Max(bounds.Width, bounds.Height);
        var bitmap = new RenderTargetBitmap(pixelSize, pixelSize, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new ScaleTransform(scale, scale));
            context.DrawDrawing(drawing);
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static DrawingImage CreateDrawingImage()
    {
        var group = new DrawingGroup();

        var background = new GeometryDrawing(
            CreateBackgroundBrush(),
            null,
            new RectangleGeometry(new Rect(Inset, Inset, CanvasSize - Inset * 2, CanvasSize - Inset * 2), CornerRadius, CornerRadius));
        background.Freeze();
        group.Children.Add(background);
        group.Children.Add(CreateMonogramDrawing());
        group.Freeze();

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static Brush CreateBackgroundBrush()
    {
        var brush = new LinearGradientBrush(
            ColorFromHex("#78909C"),
            ColorFromHex("#455A64"),
            new Point(0.12, 0.12),
            new Point(0.88, 0.88));
        brush.Freeze();
        return brush;
    }

    private static GeometryDrawing CreateMonogramDrawing()
    {
        const double fontSize = 96;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var formattedText = new FormattedText(
            "MR",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.White,
            1.0);

        var textOrigin = new Point(
            (CanvasSize - formattedText.Width) / 2,
            (CanvasSize - formattedText.Height) / 2);

        var geometry = formattedText.BuildGeometry(textOrigin);
        geometry.Freeze();

        var drawing = new GeometryDrawing(Brushes.White, null, geometry);
        drawing.Freeze();
        return drawing;
    }

    private static Color ColorFromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;
}
