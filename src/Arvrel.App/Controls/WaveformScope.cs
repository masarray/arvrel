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
    private static readonly Color PhaseRColor = Color.FromRgb(239, 74, 74);
    private static readonly Color PhaseSColor = Color.FromRgb(255, 211, 61);
    private static readonly Color PhaseTColor = Color.FromRgb(63, 145, 255);
    private static readonly Color ResidualColor = Color.FromRgb(62, 218, 120);

    private double _retainedMaximum = 1.2;
    private double _lockedTriggerSample = double.NaN;
    private int _lockedTriggerCount;
    private double _lockedPickupMarkerPosition = double.NaN;
    private double _lockedTripMarkerPosition = double.NaN;

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

        var triggerSample = ResolveLockedTriggerSample(Frame.PhaseA);
        var triggerFraction = Frame.PhaseA.Count == 0
            ? 0
            : triggerSample / Frame.PhaseA.Count;
        var pickupPosition = ResolveEvidenceMarker(
            Frame.PickupPosition,
            triggerFraction,
            ref _lockedPickupMarkerPosition);
        var tripPosition = ResolveEvidenceMarker(
            Frame.TripPosition,
            triggerFraction,
            ref _lockedTripMarkerPosition);

        DrawGrid(drawingContext, plot);
        DrawMarker(drawingContext, plot, pickupPosition, Color.FromRgb(204, 151, 52), "PICKUP");
        DrawMarker(drawingContext, plot, tripPosition, Color.FromRgb(214, 77, 72), "TRIP");

        var maximum = ResolveDisplayMaximum(Frame);
        DrawTrace(drawingContext, plot, Frame.PhaseA, maximum, PhaseRColor, triggerSample);
        DrawTrace(drawingContext, plot, Frame.PhaseB, maximum, PhaseSColor, triggerSample);
        DrawTrace(drawingContext, plot, Frame.PhaseC, maximum, PhaseTColor, triggerSample);
        DrawTrace(drawingContext, plot, Frame.Residual, maximum, ResidualColor, triggerSample, 1.7);
        DrawScale(drawingContext, plot, maximum, Frame.Frequency);
        DrawLegend(drawingContext, plot);
    }

    private double ResolveDisplayMaximum(WaveformFrame frame)
    {
        var found = false;
        var maximum = 0.0;
        AccumulateMaximum(frame.PhaseA, ref found, ref maximum);
        AccumulateMaximum(frame.PhaseB, ref found, ref maximum);
        AccumulateMaximum(frame.PhaseC, ref found, ref maximum);
        AccumulateMaximum(frame.Residual, ref found, ref maximum);

        if (!found)
        {
            _retainedMaximum = 1.2;
            return _retainedMaximum;
        }

        var target = Math.Max(1.2, maximum * 1.12);
        _retainedMaximum = target >= _retainedMaximum
            ? target
            : Math.Max(target, Math.Max(1.2, _retainedMaximum * 0.94));
        return _retainedMaximum;
    }

    private static void AccumulateMaximum(IReadOnlyList<double> values, ref bool found, ref double maximum)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var magnitude = Math.Abs(values[index]);
            if (!double.IsFinite(magnitude))
                continue;

            found = true;
            if (magnitude > maximum)
                maximum = magnitude;
        }
    }

    private double ResolveLockedTriggerSample(IReadOnlyList<double> reference)
    {
        if (reference.Count < 4)
        {
            _lockedTriggerSample = double.NaN;
            _lockedTriggerCount = reference.Count;
            return 0;
        }

        var cycleLength = reference.Count >= 8
            ? reference.Count / 2.0
            : reference.Count;
        var previousAvailable =
            _lockedTriggerCount == reference.Count &&
            double.IsFinite(_lockedTriggerSample);

        var selected = double.NaN;
        var selectedDistance = double.PositiveInfinity;
        var selectedSlope = double.NegativeInfinity;

        for (var index = 1; index < reference.Count; index++)
        {
            var previous = reference[index - 1];
            var current = reference[index];
            if (!double.IsFinite(previous) || !double.IsFinite(current) || previous > 0 || current <= 0)
                continue;

            var slope = current - previous;
            if (slope <= 0.000000001)
                continue;

            var crossing = index - 1 + (-previous / slope);
            var normalized = PositiveModulo(crossing, cycleLength);
            if (!previousAvailable)
            {
                if (normalized < selected || !double.IsFinite(selected))
                    selected = normalized;
                continue;
            }

            var distance = Math.Abs(CircularDelta(normalized, _lockedTriggerSample, cycleLength));
            if (distance < selectedDistance - 0.000001 ||
                Math.Abs(distance - selectedDistance) <= 0.000001 && slope > selectedSlope)
            {
                selected = normalized;
                selectedDistance = distance;
                selectedSlope = slope;
            }
        }

        if (!double.IsFinite(selected))
            return previousAvailable ? _lockedTriggerSample : 0;

        if (!previousAvailable)
        {
            _lockedTriggerSample = selected;
            _lockedTriggerCount = reference.Count;
            return selected;
        }

        var delta = CircularDelta(selected, _lockedTriggerSample, cycleLength);
        const double oneSampleDeadband = 1.15;
        if (Math.Abs(delta) > oneSampleDeadband)
            _lockedTriggerSample = PositiveModulo(_lockedTriggerSample + delta, cycleLength);

        return _lockedTriggerSample;
    }

    private static double ResolveEvidenceMarker(
        double normalizedPosition,
        double triggerFraction,
        ref double lockedDisplayPosition)
    {
        if (!double.IsFinite(normalizedPosition) || normalizedPosition is < 0 or > 1)
        {
            lockedDisplayPosition = double.NaN;
            return double.NaN;
        }

        if (!double.IsFinite(lockedDisplayPosition))
            lockedDisplayPosition = RotateMarker(normalizedPosition, triggerFraction);

        return lockedDisplayPosition;
    }

    private static double PositiveModulo(double value, double modulus)
    {
        if (modulus <= 0)
            return 0;
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static double CircularDelta(double value, double reference, double period)
    {
        var delta = PositiveModulo(value - reference, period);
        if (delta > period / 2)
            delta -= period;
        return delta;
    }

    private static double RotateMarker(double normalizedPosition, double triggerFraction)
    {
        if (!double.IsFinite(normalizedPosition) || normalizedPosition is < 0 or > 1)
            return normalizedPosition;

        var rotated = normalizedPosition - triggerFraction;
        if (rotated < 0)
            rotated += 1;
        return rotated;
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
        double triggerSample,
        double thickness = 1.8)
    {
        if (values.Count < 2)
            return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var displayIndex = 0; displayIndex < values.Count; displayIndex++)
            {
                var value = InterpolatedSample(values, triggerSample + displayIndex);
                var normalizedX = displayIndex / (double)(values.Count - 1);
                var x = plot.Left + normalizedX * plot.Width;
                var normalizedY = Math.Clamp(value / maximum, -1, 1);
                var y = plot.Top + plot.Height / 2 - normalizedY * plot.Height * 0.43;
                var point = new Point(x, y);
                if (displayIndex == 0)
                    context.BeginFigure(point, false, false);
                else
                    context.LineTo(point, true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), thickness), geometry);
    }

    private static double InterpolatedSample(IReadOnlyList<double> values, double position)
    {
        var count = values.Count;
        if (count == 0)
            return 0;

        position = PositiveModulo(position, count);
        var firstIndex = (int)Math.Floor(position);
        var secondIndex = (firstIndex + 1) % count;
        var fraction = position - firstIndex;
        return values[firstIndex] + (values[secondIndex] - values[firstIndex]) * fraction;
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
            (Name: "R / IA", Color: PhaseRColor, Width: 70.0),
            (Name: "S / IB", Color: PhaseSColor, Width: 70.0),
            (Name: "T / IC", Color: PhaseTColor, Width: 70.0),
            (Name: "3I0 / IN", Color: ResidualColor, Width: 82.0)
        };

        var x = plot.Right - 292;
        foreach (var entry in entries)
        {
            var brush = new SolidColorBrush(entry.Color);
            dc.DrawLine(new Pen(brush, 2), new Point(x, plot.Top + 10), new Point(x + 15, plot.Top + 10));
            dc.DrawText(CreateText(entry.Name, 9.2, brush), new Point(x + 20, plot.Top + 3));
            x += entry.Width;
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
