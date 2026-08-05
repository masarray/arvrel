namespace Arvrel.Protection;

public static class VirtualInjectionAngleOperations
{
    public static double StandardLineAngle(VirtualInjectionSignal signal) => signal switch
    {
        VirtualInjectionSignal.PhaseAVoltage or VirtualInjectionSignal.PhaseACurrent => 0,
        VirtualInjectionSignal.PhaseBVoltage or VirtualInjectionSignal.PhaseBCurrent => -120,
        VirtualInjectionSignal.PhaseCVoltage or VirtualInjectionSignal.PhaseCCurrent => 120,
        VirtualInjectionSignal.NeutralVoltage or VirtualInjectionSignal.NeutralCurrent => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, "Unknown virtual-injection signal.")
    };

    public static IReadOnlyDictionary<VirtualInjectionSignal, double> BalancedAngles(
        VirtualInjectionSignal selectedSignal,
        double selectedAngleDegrees)
    {
        if (!double.IsFinite(selectedAngleDegrees))
            throw new ArgumentOutOfRangeException(nameof(selectedAngleDegrees), "Selected angle must be finite.");

        var (phaseA, phaseB, phaseC, selectedPhaseIndex) = PhaseGroup(selectedSignal);
        var phaseAAngle = selectedPhaseIndex switch
        {
            0 => selectedAngleDegrees,
            1 => selectedAngleDegrees + 120,
            2 => selectedAngleDegrees - 120,
            _ => throw new InvalidOperationException("Unsupported phase index.")
        };

        return new Dictionary<VirtualInjectionSignal, double>
        {
            [phaseA] = VirtualInjectionChannel.NormalizeAngle(phaseAAngle),
            [phaseB] = VirtualInjectionChannel.NormalizeAngle(phaseAAngle - 120),
            [phaseC] = VirtualInjectionChannel.NormalizeAngle(phaseAAngle + 120)
        };
    }

    public static IReadOnlyDictionary<VirtualInjectionSignal, double> ReverseRotation(
        VirtualInjectionSignal selectedSignal,
        IReadOnlyDictionary<VirtualInjectionSignal, double> currentAngles)
    {
        ArgumentNullException.ThrowIfNull(currentAngles);
        var (phaseA, phaseB, phaseC, _) = PhaseGroup(selectedSignal);

        if (!currentAngles.TryGetValue(phaseA, out var phaseAAngle) ||
            !currentAngles.TryGetValue(phaseB, out var phaseBAngle) ||
            !currentAngles.TryGetValue(phaseC, out var phaseCAngle) ||
            !double.IsFinite(phaseAAngle) ||
            !double.IsFinite(phaseBAngle) ||
            !double.IsFinite(phaseCAngle))
            throw new ArgumentException("All three phase angles must be finite and available.", nameof(currentAngles));

        return new Dictionary<VirtualInjectionSignal, double>
        {
            [phaseA] = VirtualInjectionChannel.NormalizeAngle(phaseAAngle),
            [phaseB] = VirtualInjectionChannel.NormalizeAngle(phaseCAngle),
            [phaseC] = VirtualInjectionChannel.NormalizeAngle(phaseBAngle)
        };
    }

    public static bool IsThreePhaseSignal(VirtualInjectionSignal signal)
        => signal is VirtualInjectionSignal.PhaseAVoltage or
                     VirtualInjectionSignal.PhaseBVoltage or
                     VirtualInjectionSignal.PhaseCVoltage or
                     VirtualInjectionSignal.PhaseACurrent or
                     VirtualInjectionSignal.PhaseBCurrent or
                     VirtualInjectionSignal.PhaseCCurrent;

    public static IReadOnlyList<VirtualInjectionSignal> PhaseSignals(VirtualInjectionSignal signal)
    {
        var (phaseA, phaseB, phaseC, _) = PhaseGroup(signal);
        return new[] { phaseA, phaseB, phaseC };
    }

    private static (VirtualInjectionSignal PhaseA, VirtualInjectionSignal PhaseB, VirtualInjectionSignal PhaseC, int SelectedPhaseIndex)
        PhaseGroup(VirtualInjectionSignal signal) => signal switch
        {
            VirtualInjectionSignal.PhaseAVoltage => (
                VirtualInjectionSignal.PhaseAVoltage,
                VirtualInjectionSignal.PhaseBVoltage,
                VirtualInjectionSignal.PhaseCVoltage,
                0),
            VirtualInjectionSignal.PhaseBVoltage => (
                VirtualInjectionSignal.PhaseAVoltage,
                VirtualInjectionSignal.PhaseBVoltage,
                VirtualInjectionSignal.PhaseCVoltage,
                1),
            VirtualInjectionSignal.PhaseCVoltage => (
                VirtualInjectionSignal.PhaseAVoltage,
                VirtualInjectionSignal.PhaseBVoltage,
                VirtualInjectionSignal.PhaseCVoltage,
                2),
            VirtualInjectionSignal.PhaseACurrent => (
                VirtualInjectionSignal.PhaseACurrent,
                VirtualInjectionSignal.PhaseBCurrent,
                VirtualInjectionSignal.PhaseCCurrent,
                0),
            VirtualInjectionSignal.PhaseBCurrent => (
                VirtualInjectionSignal.PhaseACurrent,
                VirtualInjectionSignal.PhaseBCurrent,
                VirtualInjectionSignal.PhaseCCurrent,
                1),
            VirtualInjectionSignal.PhaseCCurrent => (
                VirtualInjectionSignal.PhaseACurrent,
                VirtualInjectionSignal.PhaseBCurrent,
                VirtualInjectionSignal.PhaseCCurrent,
                2),
            _ => throw new ArgumentOutOfRangeException(
                nameof(signal),
                signal,
                "Balanced-angle and phase-rotation operations require a three-phase voltage or current signal.")
        };
}
