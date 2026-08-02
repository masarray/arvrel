using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Arvrel.Protection;

namespace Arvrel.App.Controls;

/// <summary>
/// Small monochrome phasor instrument for the physical-relay LCD faceplate.
/// It intentionally uses the LCD ink palette instead of the coloured analysis scope.
/// </summary>
public sealed class RelayLcdPhasorScope : FrameworkElement
{
    private static readonly Color Ink = Color.FromRgb(78, 96, 117);
    private static readonly Color Grid = Color.FromArgb(82, 78, 96, 117);
    private static readonly Color MinorGrid = Color.FromArgb(43, 78, 96, 117);
    private static readonly Color Faint = Color.FromArgb(145, 78, 96, 117);

    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame),
        typeof(PhasorDisplayFrame),
        typeof(RelayLcdPhasorScope),
        new FrameworkPropertyMetadata(
            PhasorDisplayFrame.Unavailable(PhasorDisplayMode.Current, "No phasor"),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public PhasorDisplayFrame Frame
    {
        get => (PhasorDisplayFrame)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = Math.Max(1, ActualWidth);
        var height = Math.Max(1, ActualHeight);
        var header = 14.0;
        var footer = 13.0;
        var center = new Point(width / 2, header + Math.Max(20, (height - header - footer) / 2));
        var radius = Math.Max(18, Math.Min(width * 0.42, (height - header - footer) * 0.42));

        drawingContext.DrawText(Text("I PHASOR", 7.2, FontWeights.SemiBold), new Point(2, 0));
        DrawGrid(drawingContext, center, radius);

        if (!Frame.IsAvailable || Frame.Vectors.Count == 0)
        {
            DrawCenteredText(drawingContext, "NO PH", center, 7.4);
            DrawReference(drawingContext, width, height, "WAIT 4I+4U");
            return;
        }

        var maximum = Math.Max(1e-9, Frame.MaximumCurrent);
        foreach (var vector in Frame.Vectors.Where(item => item.QuantityKind == "I" && item.Magnitude > 1e-9))
        {
            var normalized = Math.Clamp(vector.Magnitude / maximum, 0, 1);
            var length = radius * (0.10 + normalized * 0.84);
            var radians = -vector.AngleDegrees * Math.PI / 180;
            var end = new Point(
                center.X + Math.Cos(radians) * length,
                center.Y + Math.Sin(radians) * length);
            var residual = vector.Key == "3I0";
            var pen = new Pen(new SolidColorBrush(Ink), residual ? 1.55 : 1.15)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            drawingContext.DrawLine(pen, center, end);
            DrawArrowHead(drawingContext, center, end, residual ? 5.3 : 4.6);
            DrawLabel(drawingContext, vector.Key, end, center, width, height);
        }

        drawingContext.DrawEllipse(new SolidColorBrush(Ink), null, center, 1.45, 1.45);
        DrawReference(drawingContext, width, height, Frame.ReferenceLabel.Replace(" = ", " "));
    }

    private static void DrawGrid(DrawingContext dc, Point center, double radius)
    {
        var major = new Pen(new SolidColorBrush(Grid), 0.75);
        var minor = new Pen(new SolidColorBrush(MinorGrid), 0.65)
        {
            DashStyle = new DashStyle(new[] { 1.5, 2.5 }, 0)
        };

        dc.DrawEllipse(null, major, center, radius, radius);
        dc.DrawEllipse(null, minor, center, radius * 0.52, radius * 0.52);

        for (var degree = 0; degree < 360; degree += 45)
        {
            var radians = -degree * Math.PI / 180;
            var end = new Point(
                center.X + Math.Cos(radians) * radius,
                center.Y + Math.Sin(radians) * radius);
            dc.DrawLine(degree % 90 == 0 ? major : minor, center, end);
        }
    }

    private static void DrawArrowHead(DrawingContext dc, Point start, Point end, double size)
    {
        var direction = start - end;
        if (direction.Length < 1)
            return;

        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var p1 = end + direction * size + normal * size * 0.40;
        var p2 = end + direction * size - normal * size * 0.40;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(end, true, true);
            context.LineTo(p1, true, false);
            context.LineTo(p2, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(new SolidColorBrush(Ink), null, geometry);
    }

    private static void DrawLabel(
        DrawingContext dc,
        string key,
        Point end,
        Point center,
        double width,
        double height)
    {
        var label = key switch
        {
            "IA" => "R",
            "IB" => "S",
            "IC" => "T",
            "3I0" => "N",
            _ => key
        };
        var text = Text(label, 7.3, FontWeights.SemiBold);
        var right = end.X >= center.X;
        var below = end.Y >= center.Y;
        var x = right ? end.X + 2 : end.X - text.Width - 2;
        var y = below ? end.Y + 1 : end.Y - text.Height - 1;

        if (key == "3I0")
            y += below ? 4 : -4;

        x = Math.Clamp(x, 1, Math.Max(1, width - text.Width - 1));
        y = Math.Clamp(y, 13, Math.Max(13, height - text.Height - 12));
        dc.DrawText(text, new Point(x, y));
    }

    private static void DrawReference(DrawingContext dc, double width, double height, string value)
    {
        var text = Text(value.ToUpperInvariant(), 6.5, FontWeights.Normal, Faint);
        dc.DrawText(text, new Point(Math.Max(1, (width - text.Width) / 2), height - text.Height - 1));
    }

    private static void DrawCenteredText(DrawingContext dc, string value, Point center, double size)
    {
        var text = Text(value, size, FontWeights.SemiBold);
        dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private static FormattedText Text(
        string value,
        double size,
        FontWeight weight,
        Color? color = null)
        => new(
            value,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            new SolidColorBrush(color ?? Ink),
            1);
}
