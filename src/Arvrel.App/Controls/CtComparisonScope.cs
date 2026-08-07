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
    IReadOnlyList<double> PhaseAFluxPerUnit,
    IReadOnlyList<double> PhaseBFluxPerUnit,
    IReadOnlyList<double> PhaseCFluxPerUnit,
    IReadOnlyList<double> NeutralFluxPerUnit,
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
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        80);
}

/// <summary>
/// Public engineering comparison of ideal referred current, relay-secondary current,
/// and the selected CT core flux against the ±1 pu knee boundary.
/// </summary>
public sealed class CtComparisonScope : FrameworkElement
{
    private static readonly Brush MutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(126, 143, 156)));
    private static readonly Brush IdealBrush = Freeze(new SolidColorBrush(Color.FromRgb(116, 130, 142)));
    private static readonly Brush SecondaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(43, 126, 183)));
    private static readonly Brush FluxBrush = Freeze(new SolidColorBrush(Color.FromRgb(134, 88, 164)));
    private static readonly Brush KneeBrush = Freeze(new SolidColorBrush(Color.FromRgb(180, 63, 58)));
    private static readonly Pen MinorPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 238)), 1));
    private static readonly Pen MajorPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(205, 215, 223)), 1));
    private static readonly Pen IdealPen = CreateIdealPen();
    private static readonly Pen SecondaryPen = Freeze(new Pen(SecondaryBrush, 1.8));
    private static readonly Pen FluxPen = Freeze(new Pen(FluxBrush, 1.7));
    private static readonly Pen KneePen = CreateKneePen();

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

        var (ideal, secondary) = ResolveChannel(Frame, SelectedChannel);
        var flux = ResolveFlux(Frame, SelectedChannel);
        var count = Math.Max(ideal.Count, secondary.Count);
        if (count < 2)
        {
            dc.DrawText(CreateText("No CT waveform evidence available", 11, MutedBrush), new Point(12, 12));
            return;
        }

        var plotWidth = Math.Max(1, width - 72);
        var availableHeight = Math.Max(180, height - 76);
        var currentHeight = Math.Max(100, availableHeight * 0.58);
        var fluxHeight = Math.Max(72, availableHeight - currentHeight - 28);
        var currentPlot = new Rect(52, 28, plotWidth, currentHeight);
        var fluxPlot = new Rect(52, currentPlot.Bottom + 28, plotWidth, fluxHeight);
        var axis = WaveformAxisLayout.Create(new WaveformAxisSource(
            count,
            Frame.Ideal.FrequencyHz,
            Frame.Ideal.SampleRateHz,
            Frame.NominalSamplesPerCycle,
            WaveformTimeOrigin.DisplayedWindowStart));

        DrawGrid(dc, currentPlot, axis, drawLabels: false);
        var maximum = ResolveMaximum(ideal, secondary);
        DrawTrace(dc, currentPlot, ideal, maximum, IdealPen);
        DrawTrace(dc, currentPlot, secondary, maximum, SecondaryPen);

        dc.DrawText(CreateText($"{SelectedChannel} · CURRENT · IDEAL dashed / RELAY SECONDARY solid", 10.5, SecondaryBrush), new Point(10, 7));
        dc.DrawText(CreateText($"±{maximum:0.##} A", 9.2, MutedBrush), new Point(5, currentPlot.Top - 2));
        dc.DrawText(CreateText("0", 9.2, MutedBrush), new Point(31, currentPlot.Top + currentPlot.Height / 2 - 7));

        DrawFluxGrid(dc, fluxPlot, axis);
        if (flux.Count >= 2)
        {
            var fluxMaximum = Math.Max(1.25, ResolveMaximumAbsolute(flux) * 1.08);
            DrawTrace(dc, fluxPlot, flux, fluxMaximum, FluxPen);
            DrawKneeBoundary(dc, fluxPlot, fluxMaximum, +1);
            DrawKneeBoundary(dc, fluxPlot, fluxMaximum, -1);
            dc.DrawText(CreateText($"CORE FLUX · ±1 pu knee · scale ±{fluxMaximum:0.##} pu", 9.8, FluxBrush), new Point(10, currentPlot.Bottom + 7));
        }
        else
        {
            dc.DrawText(
                CreateText(
                    SelectedChannel is "3I0" ? "CORE FLUX · calculated 3I0 has no single CT core" : "CORE FLUX · unavailable",
                    9.8,
                    MutedBrush),
                new Point(10, currentPlot.Bottom + 7));
        }
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

    private static IReadOnlyList<double> ResolveFlux(CtComparisonFrame frame, string channel)
        => channel switch
        {
            "IB" => frame.PhaseBFluxPerUnit,
            "IC" => frame.PhaseCFluxPerUnit,
            "IN" => frame.NeutralFluxPerUnit,
            "3I0" => Array.Empty<double>(),
            _ => frame.PhaseAFluxPerUnit
        };

    private static double ResolveMaximum(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        var maximum = Math.Max(ResolveMaximumAbsolute(first), ResolveMaximumAbsolute(second));
        return Math.Max(0.5, maximum * 1.08);
    }

    private static double ResolveMaximumAbsolute(IReadOnlyList<double> values)
    {
        var maximum = 0d;
        for (var index = 0; index < values.Count; index++)
            if (double.IsFinite(values[index]))
                maximum = Math.Max(maximum, Math.Abs(values[index]));
        return maximum;
    }

    private static void DrawGrid(DrawingContext dc, Rect plot, WaveformAxis axis, bool drawLabels)
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
            if (!drawLabels || tick.Label is null)
                continue;
            var text = CreateText(tick.Label, 8.8, MutedBrush);
            dc.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, 1, Math.Max(1, plot.Right - text.Width)), plot.Bottom + 5));
        }
    }

    private static void DrawFluxGrid(DrawingContext dc, Rect plot, WaveformAxis axis)
    {
        DrawGrid(dc, plot, axis, drawLabels: true);
        dc.DrawText(CreateText("+1", 8.8, KneeBrush), new Point(30, plot.Top + plot.Height * 0.10 - 7));
        dc.DrawText(CreateText("0", 8.8, MutedBrush), new Point(34, plot.Top + plot.Height / 2 - 7));
        dc.DrawText(CreateText("-1", 8.8, KneeBrush), new Point(30, plot.Bottom - plot.Height * 0.10 - 7));
    }

    private static void DrawKneeBoundary(DrawingContext dc, Rect plot, double maximum, double value)
    {
        var normalized = Math.Clamp(value / maximum, -1, 1);
        var y = plot.Top + plot.Height / 2 - normalized * plot.Height * 0.44;
        dc.DrawLine(KneePen, new Point(plot.Left, y), new Point(plot.Right, y));
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

    private static Pen CreateKneePen()
    {
        var pen = new Pen(KneeBrush, 1.15)
        {
            DashStyle = new DashStyle(new[] { 4d, 3d }, 0)
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
