using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arvrel.Application.Laboratory;

namespace Arvrel.Desktop.Controls;

public sealed class WaveformScope : Control
{
    public static readonly StyledProperty<ScenarioWaveform?> WaveformProperty =
        AvaloniaProperty.Register<WaveformScope, ScenarioWaveform?>(nameof(Waveform));

    private static readonly IBrush TickLabelBrush = new SolidColorBrush(Color.Parse("#8FA7B2"));
    private static readonly Pen MinorGridPen = new(new SolidColorBrush(Color.Parse("#192A33")), 1);
    private static readonly Pen MajorGridPen = new(new SolidColorBrush(Color.Parse("#243A45")), 1);
    private static readonly Pen CycleGridPen = new(new SolidColorBrush(Color.Parse("#355563")), 1.2);
    private static readonly Pen LaneBoundaryPen = new(new SolidColorBrush(Color.Parse("#20343E")), 1);
    private static readonly Pen ZeroLinePen = new(new SolidColorBrush(Color.Parse("#2F4A56")), 1.1);
    private static readonly Pen AxisPen = new(new SolidColorBrush(Color.Parse("#486571")), 1);
    private static readonly Pen PhaseAPen = new(new SolidColorBrush(Color.Parse("#55B5D0")), 1.6);
    private static readonly Pen PhaseBPen = new(new SolidColorBrush(Color.Parse("#79C88C")), 1.6);
    private static readonly Pen PhaseCPen = new(new SolidColorBrush(Color.Parse("#D9B66F")), 1.6);
    private static readonly Pen ResidualPen = new(new SolidColorBrush(Color.Parse("#D77A7A")), 1.6);
    private static readonly Typeface TickTypeface = new("Inter");

    static WaveformScope()
        => AffectsRender<WaveformScope>(WaveformProperty);

    public WaveformScope()
        => ClipToBounds = true;

    public ScenarioWaveform? Waveform
    {
        get => GetValue(WaveformProperty);
        set => SetValue(WaveformProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;
        var viewport = WaveformAxisLayout.CreateViewport(width, height);
        if (!viewport.IsRenderable)
            return;

        DrawHorizontalGrid(context, viewport);
        if (Waveform is not { } waveform)
        {
            DrawAxisBaseline(context, viewport);
            return;
        }

        var axis = WaveformAxisLayout.Create(waveform);
        DrawTimeAxis(context, axis, viewport, width);

        var maximum = waveform.PhaseA
            .Concat(waveform.PhaseB)
            .Concat(waveform.PhaseC)
            .Concat(waveform.Residual)
            .Select(Math.Abs)
            .DefaultIfEmpty(1)
            .Max();
        maximum = Math.Max(maximum, 1);

        DrawSeries(context, waveform.PhaseA, 0, maximum, viewport, PhaseAPen);
        DrawSeries(context, waveform.PhaseB, 1, maximum, viewport, PhaseBPen);
        DrawSeries(context, waveform.PhaseC, 2, maximum, viewport, PhaseCPen);
        DrawSeries(context, waveform.Residual, 3, maximum, viewport, ResidualPen);
    }

    private static void DrawHorizontalGrid(DrawingContext context, WaveformViewport viewport)
    {
        for (var row = 0; row <= 8; row++)
        {
            var y = viewport.Top + viewport.Height * row / 8d;
            var pen = row % 2 == 1 ? ZeroLinePen : LaneBoundaryPen;
            context.DrawLine(pen, new Point(viewport.Left, y), new Point(viewport.Right, y));
        }
    }

    private static void DrawTimeAxis(
        DrawingContext context,
        WaveformAxis axis,
        WaveformViewport viewport,
        double controlWidth)
    {
        foreach (var tick in axis.Ticks)
        {
            var x = viewport.Left + viewport.Width * tick.NormalizedPosition;
            var gridPen = tick.IsCycleBoundary
                ? CycleGridPen
                : tick.IsMajor
                    ? MajorGridPen
                    : MinorGridPen;

            context.DrawLine(
                gridPen,
                new Point(x, viewport.Top),
                new Point(x, viewport.Bottom));

            var tickLength = tick.IsCycleBoundary ? 7 : tick.IsMajor ? 5 : 3;
            context.DrawLine(
                AxisPen,
                new Point(x, viewport.AxisY),
                new Point(x, viewport.AxisY + tickLength));

            if (tick.Label is null)
                continue;

            var formatted = new FormattedText(
                tick.Label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                TickTypeface,
                10,
                TickLabelBrush);
            var labelX = Math.Clamp(x - formatted.Width / 2d, 1, Math.Max(1, controlWidth - formatted.Width - 1));
            context.DrawText(formatted, new Point(labelX, viewport.AxisY + 8));
        }

        DrawAxisBaseline(context, viewport);
    }

    private static void DrawAxisBaseline(DrawingContext context, WaveformViewport viewport)
        => context.DrawLine(
            AxisPen,
            new Point(viewport.Left, viewport.AxisY),
            new Point(viewport.Right, viewport.AxisY));

    private static void DrawSeries(
        DrawingContext context,
        IReadOnlyList<double> samples,
        int lane,
        double maximum,
        WaveformViewport viewport,
        Pen pen)
    {
        if (samples.Count < 2)
            return;

        var laneHeight = viewport.Height / 4d;
        var center = viewport.Top + laneHeight * (lane + 0.5);
        var scale = laneHeight * 0.38 / maximum;
        var previous = new Point(viewport.Left, center - samples[0] * scale);

        for (var index = 1; index < samples.Count; index++)
        {
            var current = new Point(
                viewport.Left + viewport.Width * index / (samples.Count - 1d),
                center - samples[index] * scale);
            context.DrawLine(pen, previous, current);
            previous = current;
        }
    }
}
