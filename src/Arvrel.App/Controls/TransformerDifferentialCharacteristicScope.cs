using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Arvrel.Protection;

namespace Arvrel.App.Controls;

/// <summary>
/// Compact practitioner characteristic view for the familiar Is1/K1/Is2/K2
/// transformer differential characteristic. The control is presentation-only;
/// all protection quantities come from TransformerProtectionSnapshot.
/// </summary>
public sealed class TransformerDifferentialCharacteristicScope : FrameworkElement
{
    private static readonly Typeface LabelTypeface = new(
        new FontFamily("Segoe UI Variable Text, Segoe UI"),
        FontStyles.Normal,
        FontWeights.Normal,
        FontStretches.Normal);

    private static readonly Typeface MonoTypeface = new(
        new FontFamily("Cascadia Mono, Consolas"),
        FontStyles.Normal,
        FontWeights.Normal,
        FontStretches.Normal);

    private static readonly Brush GridBrush = FrozenBrush(220, 228, 235);
    private static readonly Brush AxisBrush = FrozenBrush(112, 128, 141);
    private static readonly Brush CurveBrush = FrozenBrush(46, 111, 158);
    private static readonly Brush SafeFillBrush = FrozenBrush(239, 246, 250);
    private static readonly Brush OperateFillBrush = FrozenBrush(253, 242, 241);
    private static readonly Brush PhaseABrush = FrozenBrush(50, 115, 168);
    private static readonly Brush PhaseBBrush = FrozenBrush(184, 126, 37);
    private static readonly Brush PhaseCBrush = FrozenBrush(71, 145, 87);

    public TransformerDifferentialSettings? Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            InvalidateVisual();
        }
    }
    private TransformerDifferentialSettings? _settings;

    public IReadOnlyList<TransformerDifferentialPhaseSnapshot>? Phases
    {
        get => _phases;
        set
        {
            _phases = value;
            InvalidateVisual();
        }
    }
    private IReadOnlyList<TransformerDifferentialPhaseSnapshot>? _phases;

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        drawingContext.DrawRectangle(Brushes.White, null, bounds);
        if (ActualWidth < 120 || ActualHeight < 100)
            return;

        var plot = new Rect(48, 15, Math.Max(40, ActualWidth - 66), Math.Max(40, ActualHeight - 53));
        var max = ResolveAxisMaximum();
        DrawBackground(drawingContext, plot, max);
        DrawAxes(drawingContext, plot, max);
        DrawCharacteristic(drawingContext, plot, max);
        DrawPoints(drawingContext, plot, max);
    }

    private void DrawBackground(DrawingContext dc, Rect plot, double max)
    {
        var settings = Settings ?? new TransformerDifferentialSettings();
        var curve = BuildCurve(plot, max, settings);
        if (curve.Count < 2)
            return;

        var safe = new StreamGeometry();
        using (var context = safe.Open())
        {
            context.BeginFigure(new Point(plot.Left, plot.Bottom), true, true);
            context.LineTo(curve[0], true, false);
            for (var index = 1; index < curve.Count; index++)
                context.LineTo(curve[index], true, false);
            context.LineTo(new Point(plot.Right, plot.Bottom), true, false);
        }
        safe.Freeze();
        dc.DrawGeometry(SafeFillBrush, null, safe);

        var operate = new StreamGeometry();
        using (var context = operate.Open())
        {
            context.BeginFigure(new Point(plot.Left, plot.Top), true, true);
            context.LineTo(new Point(plot.Right, plot.Top), true, false);
            for (var index = curve.Count - 1; index >= 0; index--)
                context.LineTo(curve[index], true, false);
        }
        operate.Freeze();
        dc.DrawGeometry(OperateFillBrush, null, operate);
    }

    private void DrawAxes(DrawingContext dc, Rect plot, double max)
    {
        var gridPen = new Pen(GridBrush, 1);
        var axisPen = new Pen(AxisBrush, 1.1);
        for (var index = 0; index <= 5; index++)
        {
            var fraction = index / 5.0;
            var x = plot.Left + plot.Width * fraction;
            var y = plot.Bottom - plot.Height * fraction;
            dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));

            var value = max * fraction;
            DrawText(dc, value.ToString("0.#", CultureInfo.InvariantCulture), new Point(x - 8, plot.Bottom + 6), 9, AxisBrush, MonoTypeface);
            DrawText(dc, value.ToString("0.#", CultureInfo.InvariantCulture), new Point(8, y - 6), 9, AxisBrush, MonoTypeface);
        }
        dc.DrawLine(axisPen, new Point(plot.Left, plot.Top), new Point(plot.Left, plot.Bottom));
        dc.DrawLine(axisPen, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        DrawText(dc, "Ibias (pu)", new Point(plot.Right - 60, plot.Bottom + 21), 9.5, AxisBrush, LabelTypeface);
        DrawText(dc, "Idiff (pu)", new Point(5, 2), 9.5, AxisBrush, LabelTypeface);
    }

    private void DrawCharacteristic(DrawingContext dc, Rect plot, double max)
    {
        var settings = Settings ?? new TransformerDifferentialSettings();
        var points = BuildCurve(plot, max, settings);
        if (points.Count < 2)
            return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false, false);
            for (var index = 1; index < points.Count; index++)
                context.LineTo(points[index], true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(CurveBrush, 2), geometry);

        var breakpoint = Math.Clamp(settings.SlopeBreakpointPu, 0, max);
        var threshold = settings.StandardSlopeThresholdPu(breakpoint);
        var p = Map(plot, max, breakpoint, Math.Min(max, threshold));
        dc.DrawEllipse(CurveBrush, null, p, 3, 3);
        DrawText(dc, "Is2", new Point(p.X + 5, p.Y - 16), 9.5, CurveBrush, MonoTypeface);
    }

    private void DrawPoints(DrawingContext dc, Rect plot, double max)
    {
        if (Phases is null)
            return;
        foreach (var phase in Phases)
        {
            var brush = phase.Phase switch
            {
                TransformerPhase.A => PhaseABrush,
                TransformerPhase.B => PhaseBBrush,
                _ => PhaseCBrush
            };
            var point = Map(
                plot,
                max,
                Math.Clamp(phase.RestraintCurrentPu, 0, max),
                Math.Clamp(phase.OperatingCurrentPu, 0, max));
            var radius = phase.RestrainedOperated || phase.HighSetOperated ? 6.2 : phase.RestrainedPickup || phase.HighSetPickup ? 5.2 : 4.3;
            dc.DrawEllipse(brush, new Pen(Brushes.White, 1.2), point, radius, radius);
            DrawText(dc, phase.Phase.ToString(), new Point(point.X + 7, point.Y - 7), 9.5, brush, MonoTypeface);
        }
    }

    private double ResolveAxisMaximum()
    {
        var settings = Settings ?? new TransformerDifferentialSettings();
        var maximum = Math.Max(4.0, settings.SlopeBreakpointPu * 2.0);
        if (Phases is not null && Phases.Count > 0)
            maximum = Math.Max(maximum, Phases.Max(phase => Math.Max(phase.RestraintCurrentPu, phase.OperatingCurrentPu)) * 1.18);
        if (settings.HighSetEnabled)
            maximum = Math.Max(maximum, settings.HighSetPickupPu * 1.08);
        return Math.Clamp(Math.Ceiling(maximum * 2) / 2.0, 2.0, 50.0);
    }

    private static List<Point> BuildCurve(Rect plot, double max, TransformerDifferentialSettings settings)
    {
        const int samples = 96;
        var result = new List<Point>(samples + 1);
        for (var index = 0; index <= samples; index++)
        {
            var bias = max * index / samples;
            var threshold = Math.Min(max, settings.StandardSlopeThresholdPu(bias));
            result.Add(Map(plot, max, bias, threshold));
        }
        return result;
    }

    private static Point Map(Rect plot, double max, double x, double y)
        => new(
            plot.Left + plot.Width * x / max,
            plot.Bottom - plot.Height * y / max);

    private void DrawText(
        DrawingContext dc,
        string text,
        Point point,
        double size,
        Brush brush,
        Typeface typeface)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, point);
    }

    private static Brush FrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
