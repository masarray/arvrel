using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arvrel.Application.Laboratory;

namespace Arvrel.Desktop.Controls;

public sealed class WaveformScope : Control
{
    public static readonly StyledProperty<ScenarioWaveform?> WaveformProperty =
        AvaloniaProperty.Register<WaveformScope, ScenarioWaveform?>(nameof(Waveform));

    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#20323D")), 1);
    private static readonly Pen BaselinePen = new(new SolidColorBrush(Color.Parse("#2C424E")), 1);
    private static readonly Pen PhaseAPen = new(new SolidColorBrush(Color.Parse("#55B5D0")), 1.6);
    private static readonly Pen PhaseBPen = new(new SolidColorBrush(Color.Parse("#79C88C")), 1.6);
    private static readonly Pen PhaseCPen = new(new SolidColorBrush(Color.Parse("#D9B66F")), 1.6);
    private static readonly Pen ResidualPen = new(new SolidColorBrush(Color.Parse("#D77A7A")), 1.6);

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
        if (width <= 1 || height <= 1)
            return;

        DrawGrid(context, width, height);
        if (Waveform is not { } waveform)
            return;

        var maximum = waveform.PhaseA
            .Concat(waveform.PhaseB)
            .Concat(waveform.PhaseC)
            .Concat(waveform.Residual)
            .Select(Math.Abs)
            .DefaultIfEmpty(1)
            .Max();
        maximum = Math.Max(maximum, 1);

        DrawSeries(context, waveform.PhaseA, 0, maximum, width, height, PhaseAPen);
        DrawSeries(context, waveform.PhaseB, 1, maximum, width, height, PhaseBPen);
        DrawSeries(context, waveform.PhaseC, 2, maximum, width, height, PhaseCPen);
        DrawSeries(context, waveform.Residual, 3, maximum, width, height, ResidualPen);
    }

    private static void DrawGrid(DrawingContext context, double width, double height)
    {
        for (var column = 0; column <= 16; column++)
        {
            var x = width * column / 16d;
            context.DrawLine(column % 4 == 0 ? BaselinePen : GridPen, new Point(x, 0), new Point(x, height));
        }

        for (var row = 0; row <= 8; row++)
        {
            var y = height * row / 8d;
            context.DrawLine(row % 2 == 0 ? BaselinePen : GridPen, new Point(0, y), new Point(width, y));
        }
    }

    private static void DrawSeries(
        DrawingContext context,
        IReadOnlyList<double> samples,
        int lane,
        double maximum,
        double width,
        double height,
        Pen pen)
    {
        if (samples.Count < 2)
            return;

        var laneHeight = height / 4d;
        var center = laneHeight * (lane + 0.5);
        var scale = laneHeight * 0.38 / maximum;
        var previous = new Point(0, center - samples[0] * scale);

        for (var index = 1; index < samples.Count; index++)
        {
            var current = new Point(
                width * index / (samples.Count - 1d),
                center - samples[index] * scale);
            context.DrawLine(pen, previous, current);
            previous = current;
        }
    }
}
