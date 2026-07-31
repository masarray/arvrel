using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Arvrel.App.Controls;

public sealed record WaveformFrame(
    IReadOnlyList<double> PhaseA,
    IReadOnlyList<double> PhaseB,
    IReadOnlyList<double> PhaseC,
    IReadOnlyList<double> Residual,
    double Frequency,
    double PickupPosition,
    double TripPosition)
{
    public static WaveformFrame Empty { get; } = new(
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        50,
        double.NaN,
        double.NaN);
}

public sealed class WaveformScope : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame),
        typeof(WaveformFrame),
        typeof(WaveformScope),
        new FrameworkPropertyMetadata(WaveformFrame.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public WaveformFrame Frame
    {
        get => (WaveformFrame)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = Math.Max(1, ActualWidth);
        var height = Math.Max(1, ActualHeight);
        var plot = new Rect(54, 16, Math.Max(1, width - 72), Math.Max(1, height - 47));

        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(15, 24, 32)),
            new Pen(new SolidColorBrush(Color.FromRgb(52, 68, 80)), 1),
            new Rect(0.5, 0.5, width - 1, height - 1),
            4,
            4);

        DrawGrid(drawingContext, plot);
        DrawMarker(drawingContext, plot, Frame.PickupPosition, Color.FromRgb(204, 151, 52), "PICKUP");
        DrawMarker(drawingContext, plot, Frame.TripPosition, Color.FromRgb(214, 77, 72), "TRIP");

        var maximum = ResolveMaximum(Frame);
        DrawTrace(drawingContext, plot, Frame.PhaseA, maximum, Color.FromRgb(86, 192, 229));
        DrawTrace(drawingContext, plot, Frame.PhaseB, maximum, Color.FromRgb(245, 188, 81));
        DrawTrace(drawingContext, plot, Frame.PhaseC, maximum, Color.FromRgb(194, 124, 222));
        DrawTrace(drawingContext, plot, Frame.Residual, maximum, Color.FromRgb(108, 202, 137), 1.15);
        DrawScale(drawingContext, plot, maximum, Frame.Frequency);
        DrawLegend(drawingContext, plot);
    }

    private static double ResolveMaximum(WaveformFrame frame)
    {
        var maximum = new[] { frame.PhaseA, frame.PhaseB, frame.PhaseC, frame.Residual }
            .SelectMany(values => values)
            .Select(Math.Abs)
            .DefaultIfEmpty(1)
            .Max();
        return Math.Max(1.2, maximum * 1.12);
    }

    private static void DrawGrid(DrawingContext dc, Rect plot)
    {
        var minor = new Pen(new SolidColorBrush(Color.FromRgb(31, 45, 56)), 1);
        var major = new Pen(new SolidColorBrush(Color.FromRgb(45, 62, 74)), 1);
        var zero = new Pen(new SolidColorBrush(Color.FromRgb(80, 101, 115)), 1);

        for (var column = 0; column <= 16; column++)
        {
            var x = plot.Left + plot.Width * column / 16;
            dc.DrawLine(column % 4 == 0 ? major : minor, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }
        for (var row = 0; row <= 8; row++)
        {
            var y = plot.Top + plot.Height * row / 8;
            var pen = row == 4 ? zero : row % 2 == 0 ? major : minor;
            dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }
    }

    private static void DrawTrace(
        DrawingContext dc,
        Rect plot,
        IReadOnlyList<double> values,
        double maximum,
        Color color,
        double thickness = 1.55)
    {
        if (values.Count < 2)
            return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var index = 0; index < values.Count; index++)
            {
                var normalizedX = index / (double)(values.Count - 1);
                var x = plot.Left + normalizedX * plot.Width;
                var normalizedY = Math.Clamp(values[index] / maximum, -1, 1);
                var y = plot.Top + plot.Height / 2 - normalizedY * plot.Height * 0.43;
                var point = new Point(x, y);
                if (index == 0)
                    context.BeginFigure(point, false, false);
                else
                    context.LineTo(point, true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), thickness), geometry);
    }

    private static void DrawMarker(DrawingContext dc, Rect plot, double normalizedPosition, Color color, string label)
    {
        if (!double.IsFinite(normalizedPosition) || normalizedPosition is < 0 or > 1)
            return;

        var x = plot.Left + plot.Width * normalizedPosition;
        var brush = new SolidColorBrush(color);
        dc.DrawLine(new Pen(brush, 1) { DashStyle = DashStyles.Dash }, new Point(x, plot.Top), new Point(x, plot.Bottom));
        dc.DrawText(CreateText(label, 9, brush), new Point(Math.Min(x + 4, plot.Right - 48), plot.Top + 3));
    }

    private static void DrawScale(DrawingContext dc, Rect plot, double maximum, double frequency)
    {
        var muted = new SolidColorBrush(Color.FromRgb(143, 160, 173));
        dc.DrawText(CreateText($"+{maximum:0.0} A", 9.5, muted), new Point(4, plot.Top - 3));
        dc.DrawText(CreateText("0", 9.5, muted), new Point(30, plot.Top + plot.Height / 2 - 7));
        dc.DrawText(CreateText($"-{maximum:0.0} A", 9.5, muted), new Point(4, plot.Bottom - 12));
        dc.DrawText(CreateText("0 ms", 9.5, muted), new Point(plot.Left, plot.Bottom + 7));
        dc.DrawText(CreateText($"{1000 / Math.Max(1, frequency):0.0} ms", 9.5, muted), new Point(plot.Left + plot.Width / 2 - 23, plot.Bottom + 7));
        dc.DrawText(CreateText($"{2000 / Math.Max(1, frequency):0.0} ms", 9.5, muted), new Point(plot.Right - 44, plot.Bottom + 7));
    }

    private static void DrawLegend(DrawingContext dc, Rect plot)
    {
        var entries = new[]
        {
            ("IA", Color.FromRgb(86, 192, 229)),
            ("IB", Color.FromRgb(245, 188, 81)),
            ("IC", Color.FromRgb(194, 124, 222)),
            ("3I0", Color.FromRgb(108, 202, 137))
        };

        var x = plot.Right - 176;
        foreach (var (name, color) in entries)
        {
            var brush = new SolidColorBrush(color);
            dc.DrawLine(new Pen(brush, 2), new Point(x, plot.Top + 10), new Point(x + 15, plot.Top + 10));
            dc.DrawText(CreateText(name, 9.5, brush), new Point(x + 20, plot.Top + 3));
            x += 44;
        }
    }

    private static FormattedText CreateText(string text, double size, Brush brush) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface("Cascadia Mono"),
        size,
        brush,
        1);
}
