using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Arvrel.Protection;

namespace Arvrel.App.Controls;

public sealed class PhasorScope : FrameworkElement
{
    private static readonly Color CanvasColor = Color.FromRgb(15, 24, 32);
    private static readonly Color GridColor = Color.FromRgb(43, 59, 71);
    private static readonly Color MinorGridColor = Color.FromRgb(32, 46, 57);
    private static readonly Color TextColor = Color.FromRgb(177, 194, 205);
    private static readonly Color MutedColor = Color.FromRgb(117, 139, 154);
    private static readonly Color PhaseRColor = Color.FromRgb(239, 74, 74);
    private static readonly Color PhaseSColor = Color.FromRgb(255, 205, 45);
    private static readonly Color PhaseTColor = Color.FromRgb(63, 145, 255);
    private static readonly Color ResidualColor = Color.FromRgb(37, 211, 102);
    private static readonly Color SequencePositiveColor = Color.FromRgb(64, 174, 255);
    private static readonly Color SequenceNegativeColor = Color.FromRgb(255, 157, 62);

    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame),
        typeof(PhasorDisplayFrame),
        typeof(PhasorScope),
        new FrameworkPropertyMetadata(
            PhasorDisplayFrame.Unavailable(PhasorDisplayMode.Current, "Waiting for phasor data."),
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
        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(CanvasColor),
            new Pen(new SolidColorBrush(Color.FromRgb(52, 68, 80)), 1),
            new Rect(0.5, 0.5, width - 1, height - 1),
            4,
            4);

        var headerHeight = 32.0;
        var footerHeight = 38.0;
        var center = new Point(width / 2.0, headerHeight + Math.Max(20, (height - headerHeight - footerHeight) / 2.0));
        var radius = Math.Max(30, Math.Min(width * 0.37, (height - headerHeight - footerHeight) * 0.40));

        DrawHeader(drawingContext, width);
        DrawPolarGrid(drawingContext, center, radius);

        if (!Frame.IsAvailable || Frame.Vectors.Count == 0)
        {
            DrawCenteredText(drawingContext, Frame.Status, new Point(width / 2, height / 2), 10.5, MutedColor);
            DrawFooter(drawingContext, width, height);
            return;
        }

        var maxCurrent = Math.Max(1e-9, Frame.MaximumCurrent);
        var maxVoltage = Math.Max(1e-9, Frame.MaximumVoltage);
        foreach (var vector in Frame.Vectors.Where(item => item.Magnitude > 1e-9))
        {
            var maximum = vector.QuantityKind == "V" ? maxVoltage : maxCurrent;
            var normalized = Math.Clamp(vector.Magnitude / maximum, 0, 1);
            var length = radius * (0.14 + normalized * 0.82);
            var radians = -vector.AngleDegrees * Math.PI / 180.0;
            var end = new Point(
                center.X + Math.Cos(radians) * length,
                center.Y + Math.Sin(radians) * length);
            var color = ResolveVectorColor(vector);
            var isVoltage = vector.QuantityKind == "V";
            var pen = new Pen(new SolidColorBrush(color), isVoltage ? 1.65 : 2.15)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                DashStyle = isVoltage && Frame.Mode == PhasorDisplayMode.Sequence
                    ? new DashStyle(new[] { 4.0, 2.5 }, 0)
                    : DashStyles.Solid
            };

            drawingContext.DrawLine(pen, center, end);
            DrawArrowHead(drawingContext, center, end, color, isVoltage ? 7.5 : 9.0);
            DrawVectorLabel(drawingContext, vector, end, color, width, height);
        }

        drawingContext.DrawEllipse(new SolidColorBrush(Color.FromRgb(203, 214, 222)), null, center, 2.2, 2.2);
        DrawFooter(drawingContext, width, height);
    }

    private void DrawHeader(DrawingContext dc, double width)
    {
        dc.DrawText(CreateText("PHASOR", 10.3, TextColor, FontWeights.SemiBold), new Point(12, 9));
        var mode = Frame.Mode switch
        {
            PhasorDisplayMode.Current => "CURRENT",
            PhasorDisplayMode.Voltage => "VOLTAGE",
            _ => "SEQUENCE"
        };
        var modeText = CreateText(mode, 9.1, MutedColor, FontWeights.SemiBold);
        dc.DrawText(modeText, new Point(width - modeText.Width - 12, 10));
        dc.DrawLine(
            new Pen(new SolidColorBrush(Color.FromRgb(43, 59, 71)), 1),
            new Point(0, 31.5),
            new Point(width, 31.5));
    }

    private static void DrawPolarGrid(DrawingContext dc, Point center, double radius)
    {
        var major = new Pen(new SolidColorBrush(GridColor), 0.9);
        var minor = new Pen(new SolidColorBrush(MinorGridColor), 0.75)
        {
            DashStyle = new DashStyle(new[] { 2.0, 3.0 }, 0)
        };

        for (var ring = 1; ring <= 4; ring++)
        {
            var ringRadius = radius * ring / 4.0;
            dc.DrawEllipse(null, ring == 4 ? major : minor, center, ringRadius, ringRadius);
        }

        for (var degree = 0; degree < 360; degree += 30)
        {
            var radians = -degree * Math.PI / 180.0;
            var end = new Point(
                center.X + Math.Cos(radians) * radius,
                center.Y + Math.Sin(radians) * radius);
            dc.DrawLine(degree % 90 == 0 ? major : minor, center, end);
        }

        DrawAngleLabel(dc, "0°", new Point(center.X + radius + 5, center.Y - 6));
        DrawAngleLabel(dc, "+90°", new Point(center.X - 13, center.Y - radius - 17));
        DrawAngleLabel(dc, "180°", new Point(center.X - radius - 34, center.Y - 6));
        DrawAngleLabel(dc, "-90°", new Point(center.X - 13, center.Y + radius + 5));
    }

    private void DrawFooter(DrawingContext dc, double width, double height)
    {
        dc.DrawLine(
            new Pen(new SolidColorBrush(Color.FromRgb(43, 59, 71)), 1),
            new Point(0, height - 37.5),
            new Point(width, height - 37.5));

        var reference = Frame.IsAvailable ? Frame.ReferenceLabel : "NO PHASOR";
        dc.DrawText(CreateText(reference, 9.2, MutedColor), new Point(11, height - 29));

        var summary = BuildSummary();
        var summaryText = CreateText(summary, 9.2, TextColor);
        dc.DrawText(summaryText, new Point(Math.Max(11, width - summaryText.Width - 11), height - 29));

        var detail = Frame.IsAvailable ? Frame.Status : Frame.Status;
        dc.DrawText(CreateText(detail, 8.7, MutedColor), new Point(11, height - 15));
    }

    private string BuildSummary()
    {
        if (!Frame.IsAvailable)
            return "UNAVAILABLE";

        if (Frame.Mode == PhasorDisplayMode.Current)
            return $"MAX {Frame.MaximumCurrent:0.###} A";
        if (Frame.Mode == PhasorDisplayMode.Voltage)
            return $"MAX {Frame.MaximumVoltage:0.###} V";

        var angle = double.IsFinite(Frame.PositiveSequenceAngleDifferenceDegrees)
            ? $"∠I1−V1 {Frame.PositiveSequenceAngleDifferenceDegrees:+0.0;-0.0;0.0}°"
            : "∠I1−V1 —";
        return angle;
    }

    private void DrawVectorLabel(
        DrawingContext dc,
        PhasorDisplayVector vector,
        Point end,
        Color color,
        double width,
        double height)
    {
        var text = vector.Label;
        var formatted = CreateText(text, 9.3, color, FontWeights.SemiBold);
        var x = end.X >= width / 2 ? end.X + 5 : end.X - formatted.Width - 5;
        var y = end.Y >= height / 2 ? end.Y + 2 : end.Y - formatted.Height - 2;
        x = Math.Clamp(x, 5, Math.Max(5, width - formatted.Width - 5));
        y = Math.Clamp(y, 34, Math.Max(34, height - formatted.Height - 40));
        dc.DrawText(formatted, new Point(x, y));
    }

    private static Color ResolveVectorColor(PhasorDisplayVector vector)
    {
        if (vector.IsResidual)
            return ResidualColor;
        if (vector.IsNegativeSequence)
            return SequenceNegativeColor;
        if (vector.Key is "I1" or "V1")
            return SequencePositiveColor;

        return vector.Key switch
        {
            "IA" or "VA" => PhaseRColor,
            "IB" or "VB" => PhaseSColor,
            "IC" or "VC" => PhaseTColor,
            _ => TextColor
        };
    }

    private static void DrawArrowHead(DrawingContext dc, Point start, Point end, Color color, double size)
    {
        var direction = start - end;
        if (direction.Length < 1)
            return;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var p1 = end + direction * size + normal * (size * 0.42);
        var p2 = end + direction * size - normal * (size * 0.42);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(end, true, true);
            context.LineTo(p1, true, false);
            context.LineTo(p2, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(new SolidColorBrush(color), null, geometry);
    }

    private static void DrawAngleLabel(DrawingContext dc, string text, Point origin)
        => dc.DrawText(CreateText(text, 8.2, MutedColor), origin);

    private static void DrawCenteredText(DrawingContext dc, string text, Point center, double size, Color color)
    {
        var formatted = CreateText(text, size, color);
        dc.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static FormattedText CreateText(
        string text,
        double size,
        Color color,
        FontWeight? weight = null)
        => new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
            size,
            new SolidColorBrush(color),
            1);
}
