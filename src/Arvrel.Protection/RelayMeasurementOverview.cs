namespace Arvrel.Protection;

/// <summary>
/// Compact R-S-T-N measurement projection used by the relay LCD home screen.
/// Current values follow the already-scaled measurement frame, while voltage
/// magnitudes and the stable current phasor are derived from the 4I+4V phasor set.
/// </summary>
public sealed record RelayMeasurementOverview(
    IReadOnlyList<double> CurrentsRstn,
    IReadOnlyList<double> VoltagesRstn,
    PhasorDisplayFrame CurrentPhasor,
    double FrequencyHz,
    bool VoltageAvailable);

public static class RelayMeasurementOverviewProjector
{
    public static RelayMeasurementOverview Project(MeasurementFrame measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        var currents = new[]
        {
            measurement.PhaseA,
            measurement.PhaseB,
            measurement.PhaseC,
            measurement.Residual
        };

        if (measurement.Phasors is not { } phasors)
        {
            return new RelayMeasurementOverview(
                currents,
                new[] { double.NaN, double.NaN, double.NaN, double.NaN },
                PhasorDisplayFrame.Unavailable(
                    PhasorDisplayMode.Current,
                    "Current phasor unavailable until a complete 4I+4V window is present."),
                0,
                false);
        }

        var voltages = new[]
        {
            phasors.PhaseAVoltage.Magnitude,
            phasors.PhaseBVoltage.Magnitude,
            phasors.PhaseCVoltage.Magnitude,
            phasors.NeutralVoltage.Magnitude
        };

        return new RelayMeasurementOverview(
            currents,
            voltages,
            PhasorDisplayProjector.Project(phasors, PhasorDisplayMode.Current),
            phasors.FrequencyHz,
            true);
    }
}
