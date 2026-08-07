using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Arvrel.Application.Laboratory;
using Arvrel.Protection;

namespace Arvrel.App.Controls;

public sealed record CtComparisonFrame(
    VirtualInjectionCurrentReference Ideal,
    IReadOnlyList<double> PhaseASecondary,
    IReadOnlyList<double> PhaseBSecondary,
    IReadOnlyList<double> PhaseCSecondary,
    IReadOnlyList<double> NeutralOrResidualSecondary,
    int NominalSamplesPerCycle)
{
    public static CtComparisonFrame Empty { get; } = new(
        new VirtualInjectionCurrentReference(
            Array.Empty<double>(),
            Array.Empty<double>(),
            Array.Empty<double>(),
            Array.Empty<double>(),
            0,
            4_000,
            50),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        80);
}

/// <summary>
/// Public engineering comparison of the ideal referred current and the current
/// delivered to the relay after the nonlinear CT stage.
/// </summary>
public sealed class CtComparisonScope : FrameworkElement
{
    private static readonly Brush MutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(126, 143, 156)));
    private static readonly Brush IdealBrush = Freeze(new SolidColorBrush(Color.FromRgb(116, 130, 142)));
    private static readonly Brush SecondaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(43, 126, 183)));
    private static readonly Pen MinorPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 238)), 1));
    private static readonly Pen MajorPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(205, 215, 223)), 1));
    private static readonly Pen IdealPen = CreateIdealPen();
    private static readonly Pen SecondaryPen = Freeze(new Pen(SecondaryBrush, 1.8));

    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame),
        typeof(CtComparisonFrame),
        typeof(CtComparisonScope),
        new FrameworkPropertyMetadata(CtComparisonFrame.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedChannelProperty = DependencyProperty.Register(
        nameof(SelectedChannel),
        typeof(string),
        typeof(CtComparisonScope),
        new FrameworkPropertyMetadata("IA", FrameworkPropertyMetadataOptions.AffectsRender));

    public CtComparisonFrame Frame
    {
        get => (CtComparisonFrame)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public string SelectedChannel
    {
        get => (string)GetValue(SelectedChannelProperty);
        set => SetValue(SelectedChannelProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = Math.Max(1, ActualWidth);
        var height = Math.Max(1, ActualHeight);
        dc.DrawRoundedRectangle(
            Brushes.White,
            new Pen(new SolidColorBrush(Color.FromRgb(211, 220, 227)), 1),
            new Rect(0.5, 0.5, width - 1, height - 1),
            4,
            4);

        var plot = new Rect(48, 25, Math.Max(1, width - 64), Math.Max(1, height - 54));
        var (ideal, secondary) = ResolveChannel(Frame, SelectedChannel);
        var count = Math.Max(ideal.Count, secondary.Count);
        if (count < 2)
        {
            dc.DrawText(CreateText("No CT waveform evidence available", 11, MutedBrush), new Point(12, 12));
            return;
        }

        var axis = WaveformAxisLayout.Create(new WaveformAxisSource(
            count,
            Frame.Ideal.FrequencyHz,
            Frame.Ideal.SampleRateHz,
            Frame.NominalSamplesPerCycle,
            WaveformTimeOrigin.DisplayedWindowStart));
        DrawGrid(dc, plot, axis);

        var maximum = ResolveMaximum(ideal, secondary);
        DrawTrace(dc, plot, ideal, maximum, IdealPen);
        DrawTrace(dc, plot, secondary, maximum, SecondaryPen);

        dc.DrawText(CreateText($"{SelectedChannel} · IDEAL (dashed) / RELAY SECONDARY (solid)", 10.5, SecondaryBrush), new Point(10, 6));
        dc.DrawText(CreateText($"±{maximum:0.##} A", 9.5, MutedBrush), new Point(5, plot.Top - 3));
        dc.DrawText(CreateText("0", 9.5, MutedBrush), new Point(27, plot.Top + plot.Height / 2 - 7));
    }

    private static (IReadOnlyList<double> Ideal, IReadOnlyList<double> Secondary) ResolveChannel(
        CtComparisonFrame frame,
        string channel)
        => channel switch
        {
            "IB" => (frame.Ideal.PhaseB, frame.PhaseBSecondary),
            "IC" => (frame.Ideal.PhaseC, frame.PhaseCSecondary),
            "IN" or "3I0" => (frame.Ideal.NeutralOrResidual, frame.NeutralOrResidualSecondary),
            _ => (frame.Ideal.PhaseA, frame.PhaseASecondary)
        };

    private static double ResolveMaximum(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        var maximum = 0d;
        for (var index = 0; index < first.Count; index++)
            if (double.IsFinite(first[index]))
                maximum = Math.Max(maximum, Math.Abs(first[index]));
        for (var index = 0; index < second.Count; index++)
            if (double.IsFinite(second[index]))
                maximum = Math.Max(maximum, Math.Abs(second[index]));
        return Math.Max(0.5, maximum * 1.08);
    }

    private static void DrawGrid(DrawingContext dc, Rect plot, WaveformAxis axis)
    {
        for (var row = 0; row <= 4; row++)
        {
            var y = plot.Top + plot.Height * row / 4;
            dc.DrawLine(row == 2 ? MajorPen : MinorPen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        foreach (var tick in axis.Ticks)
        {
            var x = plot.Left + plot.Width * tick.NormalizedPosition;
            dc.DrawLine(tick.IsMajor ? MajorPen : MinorPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            if (tick.Label is null)
                continue;
            var text = CreateText(tick.Label, 8.8, MutedBrush);
            dc.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, 1, Math.Max(1, plot.Right - text.Width)), plot.Bottom + 5));
        }
    }

    private static void DrawTrace(
        DrawingContext dc,
        Rect plot,
        IReadOnlyList<double> values,
        double maximum,
        Pen pen)
    {
        if (values.Count < 2)
            return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var index = 0; index < values.Count; index++)
            {
                var normalizedX = WaveformAxisLayout.NormalizeSamplePosition(index, values.Count);
                var x = plot.Left + plot.Width * normalizedX;
                var normalizedY = Math.Clamp(values[index] / maximum, -1, 1);
                var y = plot.Top + plot.Height / 2 - normalizedY * plot.Height * 0.44;
                var point = new Point(x, y);
                if (index == 0)
                    context.BeginFigure(point, false, false);
                else
                    context.LineTo(point, true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static FormattedText CreateText(string text, double size, Brush brush)
        => new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            1);

    private static Pen CreateIdealPen()
    {
        var pen = new Pen(IdealBrush, 1.35)
        {
            DashStyle = new DashStyle(new[] { 5d, 3d }, 0)
        };
        pen.Freeze();
        return pen;
    }

    private static T Freeze<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }
}
