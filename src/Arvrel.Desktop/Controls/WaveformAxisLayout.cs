using System.Globalization;
using Arvrel.Application.Laboratory;

namespace Arvrel.Desktop.Controls;

/// <summary>
/// Creates a frequency-aware time axis for the waveform evidence viewport.
/// Geometry is expressed as normalized positions so it remains stable across
/// operating systems, DPI scales, and control sizes.
/// </summary>
public static class WaveformAxisLayout
{
    public const double PlotLeftInset = 10;
    public const double PlotRightInset = 10;
    public const double PlotTopInset = 6;
    public const double AxisBandHeight = 28;

    public static WaveformAxis Create(ScenarioWaveform waveform)
    {
        ArgumentNullException.ThrowIfNull(waveform);

        var sampleCount = new[]
        {
            waveform.PhaseA.Length,
            waveform.PhaseB.Length,
            waveform.PhaseC.Length,
            waveform.Residual.Length
        }.Max();

        if (sampleCount < 2)
            return WaveformAxis.Empty;
        if (!double.IsFinite(waveform.FrequencyHz) || waveform.FrequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(waveform), "Frequency must be finite and positive.");
        if (!double.IsFinite(waveform.SampleRateHz) || waveform.SampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(waveform), "Sample rate must be finite and positive.");
        if (waveform.SamplesPerCycle < 2)
            throw new ArgumentOutOfRangeException(nameof(waveform), "Samples per cycle must be at least two.");

        var durationMilliseconds = sampleCount * 1000d / waveform.SampleRateHz;
        var cycleMilliseconds = waveform.SamplesPerCycle * 1000d / waveform.SampleRateHz;
        if (!double.IsFinite(durationMilliseconds) || durationMilliseconds <= 0 ||
            !double.IsFinite(cycleMilliseconds) || cycleMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waveform), "Waveform timing metadata is invalid.");
        }

        var quarterCycleMilliseconds = cycleMilliseconds / 4d;
        var quarterSteps = Math.Max(
            1,
            (int)Math.Floor(durationMilliseconds / quarterCycleMilliseconds + 1e-9));
        var ticks = new List<WaveformTick>(quarterSteps + 2);

        for (var step = 0; step <= quarterSteps; step++)
        {
            var timeMilliseconds = step * quarterCycleMilliseconds;
            if (timeMilliseconds > durationMilliseconds + 1e-9)
                break;

            var normalizedPosition = Math.Clamp(timeMilliseconds / durationMilliseconds, 0, 1);
            var isCycleBoundary = step % 4 == 0;
            var isMajor = step % 2 == 0;
            var label = isMajor
                ? $"{FormatMilliseconds(timeMilliseconds)} ms"
                : null;

            ticks.Add(new WaveformTick(
                timeMilliseconds,
                normalizedPosition,
                isMajor,
                isCycleBoundary,
                label));
        }

        if (ticks.Count == 0 || ticks[^1].NormalizedPosition < 1 - 1e-9)
        {
            ticks.Add(new WaveformTick(
                durationMilliseconds,
                1,
                true,
                IsApproximatelyCycleBoundary(durationMilliseconds, cycleMilliseconds),
                $"{FormatMilliseconds(durationMilliseconds)} ms"));
        }

        return new WaveformAxis(
            durationMilliseconds,
            cycleMilliseconds,
            ticks);
    }

    public static WaveformViewport CreateViewport(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            return WaveformViewport.Empty;

        var plotWidth = Math.Max(0, width - PlotLeftInset - PlotRightInset);
        var plotHeight = Math.Max(0, height - PlotTopInset - AxisBandHeight);
        return new WaveformViewport(
            PlotLeftInset,
            PlotTopInset,
            plotWidth,
            plotHeight,
            PlotTopInset + plotHeight);
    }

    private static bool IsApproximatelyCycleBoundary(double timeMilliseconds, double cycleMilliseconds)
    {
        var cycles = timeMilliseconds / cycleMilliseconds;
        return Math.Abs(cycles - Math.Round(cycles)) < 1e-6;
    }

    private static string FormatMilliseconds(double value)
    {
        var roundedInteger = Math.Round(value);
        if (Math.Abs(value - roundedInteger) < 0.005)
            return roundedInteger.ToString("0", CultureInfo.InvariantCulture);

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}

public sealed record WaveformAxis(
    double DurationMilliseconds,
    double CycleMilliseconds,
    IReadOnlyList<WaveformTick> Ticks)
{
    public static WaveformAxis Empty { get; } = new(0, 0, Array.Empty<WaveformTick>());
}

public sealed record WaveformTick(
    double TimeMilliseconds,
    double NormalizedPosition,
    bool IsMajor,
    bool IsCycleBoundary,
    string? Label);

public readonly record struct WaveformViewport(
    double Left,
    double Top,
    double Width,
    double Height,
    double AxisY)
{
    public static WaveformViewport Empty { get; } = new(0, 0, 0, 0, 0);
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public bool IsRenderable => Width > 1 && Height > 1;
}
