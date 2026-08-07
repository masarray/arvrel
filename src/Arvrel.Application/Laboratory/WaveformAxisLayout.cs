using System.Globalization;

namespace Arvrel.Application.Laboratory;

/// <summary>
/// Framework-neutral waveform time-axis contract used by the public WPF shell.
/// The horizontal axis always represents elapsed time on the sampling grid. Signal-cycle
/// markers are derived from the waveform frequency, while the nominal estimator cycle is
/// retained separately for off-nominal diagnostics.
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

        return Create(new WaveformAxisSource(
            sampleCount,
            waveform.FrequencyHz,
            waveform.SampleRateHz,
            waveform.SamplesPerCycle,
            WaveformTimeOrigin.DisplayedWindowStart));
    }

    public static WaveformAxis Create(WaveformAxisSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.SampleCount < 2)
            return WaveformAxis.Empty with { TimeOrigin = source.TimeOrigin };
        if (!double.IsFinite(source.SignalFrequencyHz) || source.SignalFrequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(source), "Signal frequency must be finite and positive.");
        if (!double.IsFinite(source.SampleRateHz) || source.SampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(source), "Sample rate must be finite and positive.");
        if (source.NominalSamplesPerCycle < 2)
            throw new ArgumentOutOfRangeException(nameof(source), "Nominal samples per cycle must be at least two.");

        // Duration is the complete interval represented by the sample buffer. The last
        // sample timestamp is one sampling interval before the right-hand window boundary.
        var durationMilliseconds = source.SampleCount * 1_000d / source.SampleRateHz;
        var lastSampleTimestampMilliseconds = (source.SampleCount - 1d) * 1_000d / source.SampleRateHz;
        var signalCycleMilliseconds = 1_000d / source.SignalFrequencyHz;
        var nominalCycleMilliseconds = source.NominalSamplesPerCycle * 1_000d / source.SampleRateHz;
        if (!double.IsFinite(durationMilliseconds) || durationMilliseconds <= 0 ||
            !double.IsFinite(lastSampleTimestampMilliseconds) || lastSampleTimestampMilliseconds < 0 ||
            !double.IsFinite(signalCycleMilliseconds) || signalCycleMilliseconds <= 0 ||
            !double.IsFinite(nominalCycleMilliseconds) || nominalCycleMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(source), "Waveform timing metadata is invalid.");
        }

        var quarterCycleMilliseconds = signalCycleMilliseconds / 4d;
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
                false,
                label));
        }

        // A non-integer-cycle window still needs an explicit right-edge timestamp. This
        // boundary is not presented as a signal-cycle marker unless it truly coincides.
        if (ticks.Count == 0 || ticks[^1].NormalizedPosition < 1 - 1e-9)
        {
            ticks.Add(new WaveformTick(
                durationMilliseconds,
                1,
                true,
                IsApproximatelyCycleBoundary(durationMilliseconds, signalCycleMilliseconds),
                true,
                $"{FormatMilliseconds(durationMilliseconds)} ms"));
        }
        else if (ticks.Count > 0 && Math.Abs(ticks[^1].NormalizedPosition - 1) < 1e-9)
        {
            ticks[^1] = ticks[^1] with { IsWindowBoundary = true };
        }

        return new WaveformAxis(
            durationMilliseconds,
            lastSampleTimestampMilliseconds,
            signalCycleMilliseconds,
            nominalCycleMilliseconds,
            source.TimeOrigin,
            ticks);
    }

    /// <summary>
    /// Returns the true sample timestamp normalized to the complete buffer interval.
    /// The final sample is therefore one sample interval before the right window edge.
    /// </summary>
    public static double NormalizeSamplePosition(int sampleIndex, int sampleCount)
    {
        if (sampleCount <= 0)
            return 0;
        if (sampleIndex < 0 || sampleIndex >= sampleCount)
            throw new ArgumentOutOfRangeException(nameof(sampleIndex));

        return sampleIndex / (double)sampleCount;
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

public enum WaveformTimeOrigin
{
    /// <summary>
    /// t = 0 is the sample currently rendered at the left edge. A renderer may stabilize
    /// that sample to a trigger crossing, but the axis always remains relative to what is displayed.
    /// </summary>
    DisplayedWindowStart
}

public sealed record WaveformAxisSource(
    int SampleCount,
    double SignalFrequencyHz,
    double SampleRateHz,
    int NominalSamplesPerCycle,
    WaveformTimeOrigin TimeOrigin);

public sealed record WaveformAxis(
    double DurationMilliseconds,
    double LastSampleTimestampMilliseconds,
    double SignalCycleMilliseconds,
    double NominalCycleMilliseconds,
    WaveformTimeOrigin TimeOrigin,
    IReadOnlyList<WaveformTick> Ticks)
{
    public static WaveformAxis Empty { get; } = new(
        0,
        0,
        0,
        0,
        WaveformTimeOrigin.DisplayedWindowStart,
        Array.Empty<WaveformTick>());

    public bool UsesOffNominalCycleGrid =>
        SignalCycleMilliseconds > 0 &&
        NominalCycleMilliseconds > 0 &&
        Math.Abs(SignalCycleMilliseconds - NominalCycleMilliseconds) > 1e-6;
}

public sealed record WaveformTick(
    double TimeMilliseconds,
    double NormalizedPosition,
    bool IsMajor,
    bool IsCycleBoundary,
    bool IsWindowBoundary,
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
