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
    // R-S-T-N phase identification used by the protection waveform view.
    private static readonly Color PhaseRColor = Color.FromRgb(239, 74, 74);
    private static readonly Color PhaseSColor = Color.FromRgb(255, 211, 61);
    private static readonly Color PhaseTColor = Color.FromRgb(63, 145, 255);
    private static readonly Color NeutralColor = Color.FromRgb(17, 22, 27);
    private static readonly Color NeutralHaloColor = Color.FromRgb(207, 216, 222);

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
        DrawTrace(drawingContext, plot, Frame.PhaseA, maximum, PhaseRColor);
        DrawTrace(drawingContext, plot, Frame.PhaseB, maximum, PhaseSColor);
        DrawTrace(drawingContext, plot, Frame.PhaseC, maximum, PhaseTColor);
        DrawTrace(
            drawingContext,
            plot,
            Frame.Residual,
            maximum,
            NeutralColor,
            1.55,
            NeutralHaloColor,
            3.55);
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
        double thickness = 1.75,
        Color? haloColor = null,
        double haloThickness = 0)
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
        if (haloColor.HasValue && haloThickness > thickness)
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(haloColor.Value), haloThickness), geometry);
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
            (Name: "R / IA", Color: PhaseRColor, Neutral: false),
            (Name: "S / IB", Color: PhaseSColor, Neutral: false),
            (Name: "T / IC", Color: PhaseTColor, Neutral: false),
            (Name: "N / 3I0", Color: NeutralColor, Neutral: true)
        };

        var x = plot.Right - 286;
        foreach (var entry in entries)
        {
            var brush = new SolidColorBrush(entry.Color);
            if (entry.Neutral)
            {
                dc.DrawLine(
                    new Pen(new SolidColorBrush(NeutralHaloColor), 4),
                    new Point(x, plot.Top + 10),
                    new Point(x + 15, plot.Top + 10));
            }

            dc.DrawLine(new Pen(brush, 2), new Point(x, plot.Top + 10), new Point(x + 15, plot.Top + 10));
            var textBrush = entry.Neutral ? new SolidColorBrush(NeutralHaloColor) : brush;
            dc.DrawText(CreateText(entry.Name, 9.2, textBrush), new Point(x + 20, plot.Top + 3));
            x += entry.Neutral ? 76 : 70;
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
