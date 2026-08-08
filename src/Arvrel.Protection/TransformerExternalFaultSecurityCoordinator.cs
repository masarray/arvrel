namespace Arvrel.Protection;

internal readonly record struct TransformerExternalFaultPhaseDecision(
    bool Armed,
    bool HighVoltageSaturationSuspected,
    bool LowVoltageSaturationSuspected,
    bool HighVoltageBlocked,
    bool LowVoltageBlocked)
{
    public bool Blocked => HighVoltageBlocked || LowVoltageBlocked;

    public static TransformerExternalFaultPhaseDecision Ready { get; } = new(false, false, false, false, false);
}

/// <summary>
/// Stateful vendor-neutral external-fault supervisor. It arms only when restraint
/// rises materially while differential current remains small, then converts later
/// CT waveform-distortion evidence into a temporary security hold. Distortion by
/// itself never blocks protection.
/// </summary>
internal sealed class TransformerExternalFaultSecurityCoordinator
{
    private readonly bool[] _historyInitialized = new bool[3];
    private readonly double[] _previousOperating = new double[3];
    private readonly double[] _previousRestraint = new double[3];
    private readonly DateTimeOffset?[] _armUntil = new DateTimeOffset?[3];
    private readonly DateTimeOffset?[] _highVoltageHoldUntil = new DateTimeOffset?[3];
    private readonly DateTimeOffset?[] _lowVoltageHoldUntil = new DateTimeOffset?[3];

    public TransformerExternalFaultPhaseDecision EvaluatePhase(
        int index,
        DateTimeOffset timestamp,
        double operating,
        double restraint,
        TransformerCtSaturationPhaseEvidence highVoltageEvidence,
        TransformerCtSaturationPhaseEvidence lowVoltageEvidence,
        TransformerExternalFaultSecuritySettings settings,
        bool measurementAvailable)
    {
        if (!settings.Enabled || !measurementAvailable)
        {
            ResetPhase(index);
            return TransformerExternalFaultPhaseDecision.Ready;
        }

        var highVoltageSaturation = IsCtSaturationSuspected(highVoltageEvidence, settings);
        var lowVoltageSaturation = IsCtSaturationSuspected(lowVoltageEvidence, settings);

        if (!_historyInitialized[index])
        {
            _historyInitialized[index] = true;
            _previousOperating[index] = operating;
            _previousRestraint[index] = restraint;
            return new TransformerExternalFaultPhaseDecision(
                false,
                highVoltageSaturation,
                lowVoltageSaturation,
                false,
                false);
        }

        var biasIncrease = restraint - _previousRestraint[index];
        var differentialRatio = operating / Math.Max(restraint, 1e-9);
        var externalFaultCandidate =
            restraint >= settings.MinimumBiasPu &&
            biasIncrease >= settings.MinimumBiasIncreasePu &&
            differentialRatio <= settings.MaximumInitialDifferentialToBiasRatio;

        if (externalFaultCandidate)
            _armUntil[index] = timestamp + settings.ArmingDuration;

        var armed = HoldActive(_armUntil[index], timestamp);
        if (!armed)
            _armUntil[index] = null;

        if (armed && highVoltageSaturation)
            _highVoltageHoldUntil[index] = ExtendHold(_highVoltageHoldUntil[index], timestamp + settings.SecurityHold);
        if (armed && lowVoltageSaturation)
            _lowVoltageHoldUntil[index] = ExtendHold(_lowVoltageHoldUntil[index], timestamp + settings.SecurityHold);

        var highVoltageBlocked = HoldActive(_highVoltageHoldUntil[index], timestamp);
        var lowVoltageBlocked = HoldActive(_lowVoltageHoldUntil[index], timestamp);
        if (!highVoltageBlocked)
            _highVoltageHoldUntil[index] = null;
        if (!lowVoltageBlocked)
            _lowVoltageHoldUntil[index] = null;

        _previousOperating[index] = operating;
        _previousRestraint[index] = restraint;

        return new TransformerExternalFaultPhaseDecision(
            armed,
            highVoltageSaturation,
            lowVoltageSaturation,
            highVoltageBlocked,
            lowVoltageBlocked);
    }

    public static TransformerExternalFaultSecuritySnapshot BuildSnapshot(
        IReadOnlyList<TransformerDifferentialPhaseSnapshot> phases,
        TransformerExternalFaultSecuritySettings settings)
    {
        if (!settings.Enabled)
            return TransformerExternalFaultSecuritySnapshot.Disabled;

        var snapshots = phases.Select(phase => new TransformerExternalFaultSecurityPhaseSnapshot(
            phase.Phase,
            phase.ExternalFaultArmed,
            phase.HighVoltageCtSaturationSuspected,
            phase.LowVoltageCtSaturationSuspected,
            phase.HighVoltageExternalFaultBlocked,
            phase.LowVoltageExternalFaultBlocked,
            phase.HighVoltageCtDistortionRatio,
            phase.LowVoltageCtDistortionRatio,
            phase.HighVoltageCtPeakAsymmetry,
            phase.LowVoltageCtPeakAsymmetry,
            phase.Reason)).ToArray();
        var anyArmed = snapshots.Any(phase => phase.Armed);
        var anyBlocked = snapshots.Any(phase => phase.Blocked);
        var refHighVoltageBlocked = settings.SuperviseRef && snapshots.Any(phase => phase.HighVoltageBlocked);
        var refLowVoltageBlocked = settings.SuperviseRef && snapshots.Any(phase => phase.LowVoltageBlocked);
        var reason = anyBlocked
            ? "Restraint-leading external-fault sequence followed by CT waveform-distortion evidence; dynamic security hold active."
            : anyArmed
                ? "Restraint-leading external-fault candidate armed; no CT saturation security block yet."
                : snapshots.Any(phase => phase.HighVoltageSaturationSuspected || phase.LowVoltageSaturationSuspected)
                    ? "CT waveform distortion observed without an armed external-fault sequence; protection remains available."
                    : "External-fault CT saturation security ready.";

        return new TransformerExternalFaultSecuritySnapshot(
            true,
            anyArmed,
            anyBlocked,
            refHighVoltageBlocked,
            refLowVoltageBlocked,
            snapshots,
            reason);
    }

    public void Reset()
    {
        Array.Fill(_historyInitialized, false);
        Array.Fill(_previousOperating, 0);
        Array.Fill(_previousRestraint, 0);
        Array.Fill<DateTimeOffset?>(_armUntil, null);
        Array.Fill<DateTimeOffset?>(_highVoltageHoldUntil, null);
        Array.Fill<DateTimeOffset?>(_lowVoltageHoldUntil, null);
    }

    private void ResetPhase(int index)
    {
        _historyInitialized[index] = false;
        _previousOperating[index] = 0;
        _previousRestraint[index] = 0;
        _armUntil[index] = null;
        _highVoltageHoldUntil[index] = null;
        _lowVoltageHoldUntil[index] = null;
    }

    private static bool IsCtSaturationSuspected(
        TransformerCtSaturationPhaseEvidence evidence,
        TransformerExternalFaultSecuritySettings settings)
    {
        if (!evidence.Reliable)
            return false;
        if (evidence.DistortionRatio >= settings.SevereDistortionRatioThreshold)
            return true;
        return evidence.DistortionRatio >= settings.DistortionRatioThreshold &&
               evidence.PeakAsymmetry >= settings.PeakAsymmetryThreshold;
    }

    private static DateTimeOffset ExtendHold(DateTimeOffset? current, DateTimeOffset candidate)
        => current is { } existing && existing > candidate ? existing : candidate;

    private static bool HoldActive(DateTimeOffset? holdUntil, DateTimeOffset timestamp)
        => holdUntil is { } value && timestamp <= value;
}
