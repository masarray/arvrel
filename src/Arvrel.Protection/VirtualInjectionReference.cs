namespace Arvrel.Protection;

public sealed record VirtualInjectionCurrentReference(
    double[] PhaseA,
    double[] PhaseB,
    double[] PhaseC,
    double[] NeutralOrResidual,
    long StartSampleIndex,
    double SampleRateHz,
    double FrequencyHz);

/// <summary>
/// Produces the ideal referred-secondary current trajectory used as the public
/// comparison reference for nonlinear CT output. No CT state is advanced.
/// </summary>
public static class VirtualInjectionReferenceGenerator
{
    public static VirtualInjectionCurrentReference GenerateCurrents(
        VirtualInjectionProfile profile,
        long startSampleIndex,
        int sampleCount,
        double sampleRateHz)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (startSampleIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startSampleIndex));
        if (sampleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        if (!double.IsFinite(sampleRateHz) || sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));

        profile = profile.Normalize();
        var phaseA = GenerateChannel(profile.PhaseACurrent, startSampleIndex, sampleCount, sampleRateHz, profile.FrequencyHz);
        var phaseB = GenerateChannel(profile.PhaseBCurrent, startSampleIndex, sampleCount, sampleRateHz, profile.FrequencyHz);
        var phaseC = GenerateChannel(profile.PhaseCCurrent, startSampleIndex, sampleCount, sampleRateHz, profile.FrequencyHz);
        var neutral = profile.ExplicitNeutralCurrent
            ? GenerateChannel(profile.NeutralCurrent, startSampleIndex, sampleCount, sampleRateHz, profile.FrequencyHz)
            : Sum(phaseA, phaseB, phaseC);

        return new VirtualInjectionCurrentReference(
            phaseA,
            phaseB,
            phaseC,
            neutral,
            startSampleIndex,
            sampleRateHz,
            profile.FrequencyHz);
    }

    private static double[] GenerateChannel(
        VirtualInjectionChannel channel,
        long startSampleIndex,
        int count,
        double sampleRateHz,
        double injectedFrequencyHz)
    {
        if (!channel.Enabled)
            return new double[count];

        var result = new double[count];
        var peak = channel.Rms * Math.Sqrt(2);
        var phase = VirtualInjectionChannel.NormalizeAngle(channel.AngleDegrees) * Math.PI / 180;
        var dcFraction = channel.DcOffsetPercent / 100d;
        var dcTimeConstantSeconds = channel.DcTimeConstantMilliseconds / 1_000d;
        for (var index = 0; index < count; index++)
        {
            var absoluteSampleIndex = checked(startSampleIndex + index);
            var timeSeconds = absoluteSampleIndex / sampleRateHz;
            var angle = 2 * Math.PI * injectedFrequencyHz * timeSeconds;
            result[index] = peak * Math.Cos(angle + phase) +
                peak * dcFraction * Math.Exp(-timeSeconds / dcTimeConstantSeconds);
        }
        return result;
    }

    private static double[] Sum(double[] first, double[] second, double[] third)
    {
        var result = new double[first.Length];
        for (var index = 0; index < result.Length; index++)
            result[index] = first[index] + second[index] + third[index];
        return result;
    }
}
