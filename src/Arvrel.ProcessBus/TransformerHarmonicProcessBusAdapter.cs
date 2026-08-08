using Arvrel.Protection;

namespace Arvrel.ProcessBus;

/// <summary>
/// P10/P13 waveform-evidence adapter. It enriches an already validated HV/LV SV pair
/// with measured H2/H5 ratios plus vendor-neutral CT waveform-distortion evidence.
/// Alignment remains owned by TransformerProcessBusAdapter; protection decisions remain
/// inside TransformerProtectionEngine.
/// </summary>
public sealed class TransformerHarmonicProcessBusAdapter
{
    private readonly SmvProcessBusController _controller;
    private readonly TransformerSvPairingSettings _pairingSettings;
    private readonly TransformerHarmonicEstimatorSettings _harmonicSettings;

    public TransformerHarmonicProcessBusAdapter(
        SmvProcessBusController controller,
        TransformerSvPairingSettings? pairingSettings = null,
        TransformerHarmonicEstimatorSettings? harmonicSettings = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _pairingSettings = pairingSettings ?? new TransformerSvPairingSettings();
        _harmonicSettings = harmonicSettings ?? new TransformerHarmonicEstimatorSettings();
        _pairingSettings.Validate();
        _harmonicSettings.Validate();
    }

    public TransformerSvPairSnapshot ReadPair(string highVoltageStreamKey, string lowVoltageStreamKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(highVoltageStreamKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(lowVoltageStreamKey);

        var highVoltage = _controller.GetSnapshot(highVoltageStreamKey, primaryDisplay: false);
        var lowVoltage = _controller.GetSnapshot(lowVoltageStreamKey, primaryDisplay: false);
        return AlignAndEstimate(highVoltage, lowVoltage, _pairingSettings, _harmonicSettings);
    }

    public static TransformerSvPairSnapshot AlignAndEstimate(
        SmvRuntimeSnapshot highVoltage,
        SmvRuntimeSnapshot lowVoltage,
        TransformerSvPairingSettings? pairingSettings = null,
        TransformerHarmonicEstimatorSettings? harmonicSettings = null)
    {
        ArgumentNullException.ThrowIfNull(highVoltage);
        ArgumentNullException.ThrowIfNull(lowVoltage);

        var pair = TransformerProcessBusAdapter.AlignSnapshots(highVoltage, lowVoltage, pairingSettings);
        if (!pair.IsAligned || pair.Measurement is null)
            return pair;

        var policy = harmonicSettings ?? new TransformerHarmonicEstimatorSettings();
        policy.Validate();
        var ctPolicy = new TransformerCtSaturationEstimatorSettings
        {
            WindowCycles = policy.WindowCycles,
            MinimumSamplesPerCycle = policy.MinimumSamplesPerCycle,
            MinimumFundamentalRms = policy.MinimumFundamentalRms
        };

        try
        {
            var highVoltageHarmonics = TransformerHarmonicEstimator.EstimateThreePhase(
                highVoltage.Waveform.PhaseA,
                highVoltage.Waveform.PhaseB,
                highVoltage.Waveform.PhaseC,
                highVoltage.SamplesPerCycle,
                policy);
            var lowVoltageHarmonics = TransformerHarmonicEstimator.EstimateThreePhase(
                lowVoltage.Waveform.PhaseA,
                lowVoltage.Waveform.PhaseB,
                lowVoltage.Waveform.PhaseC,
                lowVoltage.SamplesPerCycle,
                policy);
            var highVoltageCtEvidence = TransformerCtSaturationEstimator.EstimateThreePhase(
                highVoltage.Waveform.PhaseA,
                highVoltage.Waveform.PhaseB,
                highVoltage.Waveform.PhaseC,
                highVoltage.SamplesPerCycle,
                ctPolicy);
            var lowVoltageCtEvidence = TransformerCtSaturationEstimator.EstimateThreePhase(
                lowVoltage.Waveform.PhaseA,
                lowVoltage.Waveform.PhaseB,
                lowVoltage.Waveform.PhaseC,
                lowVoltage.SamplesPerCycle,
                ctPolicy);

            var highVoltageMeasurement = pair.Measurement.HighVoltage with
            {
                SecondHarmonicRatio = highVoltageHarmonics.SecondRatios,
                FifthHarmonicRatio = highVoltageHarmonics.FifthRatios,
                CtSaturationEvidence = highVoltageCtEvidence
            };
            var lowVoltageMeasurement = pair.Measurement.LowVoltage with
            {
                SecondHarmonicRatio = lowVoltageHarmonics.SecondRatios,
                FifthHarmonicRatio = lowVoltageHarmonics.FifthRatios,
                CtSaturationEvidence = lowVoltageCtEvidence
            };
            var measurement = pair.Measurement with
            {
                HighVoltage = highVoltageMeasurement,
                LowVoltage = lowVoltageMeasurement
            };

            var harmonicReliable = CountReliable(highVoltageHarmonics) + CountReliable(lowVoltageHarmonics);
            var ctReliable = highVoltageCtEvidence.ReliablePhaseCount + lowVoltageCtEvidence.ReliablePhaseCount;
            var detail = $"{pair.Diagnostics.Detail} H2/H5 coherent DFT ratios available on {harmonicReliable}/6 phase inputs; CT waveform evidence available on {ctReliable}/6.";
            return pair with
            {
                Measurement = measurement,
                Diagnostics = pair.Diagnostics with { Detail = detail }
            };
        }
        catch (ArgumentException ex)
        {
            return HarmonicUnavailable(pair, ex.Message);
        }
    }

    private static int CountReliable(TransformerThreePhaseHarmonicEstimate estimate)
    {
        var count = 0;
        if (estimate.PhaseA.RatioReliable) count++;
        if (estimate.PhaseB.RatioReliable) count++;
        if (estimate.PhaseC.RatioReliable) count++;
        return count;
    }

    private static TransformerSvPairSnapshot HarmonicUnavailable(TransformerSvPairSnapshot pair, string detail)
    {
        return pair with
        {
            Measurement = null,
            Diagnostics = pair.Diagnostics with
            {
                Aligned = false,
                Code = "PAIR_HARMONIC_ESTIMATE_INVALID",
                Detail = $"Paired SV alignment passed, but waveform evidence is not trustworthy: {detail}"
            }
        };
    }
}
