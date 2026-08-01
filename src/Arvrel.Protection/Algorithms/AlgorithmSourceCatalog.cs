namespace Arvrel.Protection.Algorithms;

public static class AlgorithmSourceCatalog
{
    public static string Build(string element, ProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        return element switch
        {
            "50P-1" => BuildDefinite(
                "50P-1",
                "max(IA.rms1c, IB.rms1c, IC.rms1c)",
                "PhaseInstantaneousPickupA",
                "PhaseInstantaneousDelay",
                "PhaseInstantaneousDropoutRatio"),
            "51P" => BuildTime(
                "51P",
                "max(IA.rms1c, IB.rms1c, IC.rms1c)",
                settings.PhaseTimeCurve,
                settings.PhaseTimeUserK,
                settings.PhaseTimeUserAlpha,
                settings.PhaseTimeUserC,
                "PhaseTimePickupA",
                "PhaseTimeMultiplier",
                "PhaseTimeDefiniteDelay",
                "PhaseTimeMinimumOperateTime",
                "PhaseTimeDropoutRatio",
                "PhaseTimeResetMode"),
            "50N" => BuildDefinite(
                "50N",
                "current.residual.rms1c",
                "EarthInstantaneousPickupA",
                "EarthInstantaneousDelay",
                "EarthInstantaneousDropoutRatio"),
            "51N" => BuildTime(
                "51N",
                "current.residual.rms1c",
                settings.EarthTimeCurve,
                settings.EarthTimeUserK,
                settings.EarthTimeUserAlpha,
                settings.EarthTimeUserC,
                "EarthTimePickupA",
                "EarthTimeMultiplier",
                "EarthTimeDefiniteDelay",
                "EarthTimeMinimumOperateTime",
                "EarthTimeDropoutRatio",
                "EarthTimeResetMode"),
            "67P" => BuildDirectionalPhase(settings.Feeder),
            "67N" => BuildDirectionalEarth(settings.Feeder),
            "27" => BuildUndervoltage(settings.Feeder),
            "59" => BuildOvervoltage(settings.Feeder),
            "59N" => BuildResidualOvervoltage(settings.Feeder),
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Unknown protection element.")
        };
    }

    private static string BuildDefinite(
        string element,
        string measurement,
        string pickup,
        string delay,
        string dropout)
        => $$"""
            element "{{element}}" {
              measurement operatingCurrent = {{measurement}}
              pickup = operatingCurrent >= setting("{{pickup}}")
              dropout = operatingCurrent < setting("{{pickup}}") * setting("{{dropout}}")
              operate = pickup.persist(setting("{{delay}}"))
              trip = operate && smv.allowsTrip
            }
            """;

    private static string BuildTime(
        string element,
        string measurement,
        IecCurveFamily curve,
        double userK,
        double userAlpha,
        double userC,
        string pickup,
        string tms,
        string definiteDelay,
        string minimumOperate,
        string dropout,
        string resetMode)
    {
        var formula = IecCurveCalculator.Formula(curve, userK, userAlpha, userC);
        return $$"""
            element "{{element}}" {
              measurement operatingCurrent = {{measurement}}
              multiple = operatingCurrent / setting("{{pickup}}")
              characteristic = curve("{{curve}}")
              // {{formula}}
              operateTime = characteristic.evaluate(
                multiple,
                setting("{{tms}}"),
                setting("{{definiteDelay}}"),
                setting("{{minimumOperate}}"))
              progress = integrate(dt / operateTime)
                when multiple > 1
                reset using setting("{{resetMode}}")
              dropout = multiple < setting("{{dropout}}")
              trip = progress >= 1 && smv.allowsTrip
            }
            """;
    }

    private static string BuildDirectionalPhase(FeederProtectionSettings settings)
        => $$"""
            element "67P" {
              measurement operatingCurrent = max(IA.rms1c, IB.rms1c, IC.rms1c)
              phasor I1 = sequence.positive(current.fundamental)
              phasor V1 = sequence.positive(voltage.fundamental)
              polarizingAvailable = abs(V1) >= setting("Feeder.DirectionalPhase67MinimumPolarizingVoltageV")
              operatingAngle = angle(I1) - angle(V1)
              torque = cos(operatingAngle - setting("Feeder.DirectionalPhase67CharacteristicAngleDeg"))
              selectedDirection = direction(torque, "{{settings.DirectionalPhase67Sense}}")
              pickup = operatingCurrent >= setting("Feeder.DirectionalPhase67PickupA")
                && polarizingAvailable
                && selectedDirection
              dropout = operatingCurrent < setting("Feeder.DirectionalPhase67PickupA")
                * setting("Feeder.DirectionalPhase67DropoutRatio")
                || !selectedDirection
              operate = pickup.persist(setting("Feeder.DirectionalPhase67Delay"))
              trip = operate && smv.allowsTrip
            }
            """;

    private static string BuildDirectionalEarth(FeederProtectionSettings settings)
        => $$"""
            element "67N" {
              phasor operatingCurrent = 3I0.fundamental
              phasor polarizingVoltage = 3V0.fundamental
              polarizingAvailable = abs(polarizingVoltage)
                >= setting("Feeder.DirectionalEarth67NMinimumPolarizingVoltageV")
              operatingAngle = angle(operatingCurrent) - angle(polarizingVoltage)
              torque = cos(operatingAngle - setting("Feeder.DirectionalEarth67NCharacteristicAngleDeg"))
              selectedDirection = direction(torque, "{{settings.DirectionalEarth67NSense}}")
              pickup = abs(operatingCurrent) >= setting("Feeder.DirectionalEarth67NPickupA")
                && polarizingAvailable
                && selectedDirection
              dropout = abs(operatingCurrent) < setting("Feeder.DirectionalEarth67NPickupA")
                * setting("Feeder.DirectionalEarth67NDropoutRatio")
                || !selectedDirection
              operate = pickup.persist(setting("Feeder.DirectionalEarth67NDelay"))
              trip = operate && smv.allowsTrip
            }
            """;

    private static string BuildUndervoltage(FeederProtectionSettings settings)
        => $$"""
            element "27" {
              measurement voltages = voltage.select("{{settings.Undervoltage27Mode}}")
              pickup = count(voltages <= setting("Feeder.Undervoltage27PickupV"))
                >= phaseCount("{{settings.Undervoltage27Logic}}")
              dropout = selectedVoltage >= setting("Feeder.Undervoltage27PickupV")
                * setting("Feeder.Undervoltage27ResetRatio")
              operate = pickup.persist(setting("Feeder.Undervoltage27Delay"))
              trip = operate && smv.allowsTrip
            }
            """;

    private static string BuildOvervoltage(FeederProtectionSettings settings)
        => $$"""
            element "59" {
              measurement voltages = voltage.select("{{settings.Overvoltage59Mode}}")
              pickup = count(voltages >= setting("Feeder.Overvoltage59PickupV"))
                >= phaseCount("{{settings.Overvoltage59Logic}}")
              dropout = selectedVoltage < setting("Feeder.Overvoltage59PickupV")
                * setting("Feeder.Overvoltage59DropoutRatio")
              operate = pickup.persist(setting("Feeder.Overvoltage59Delay"))
              trip = operate && smv.allowsTrip
            }
            """;

    private static string BuildResidualOvervoltage(FeederProtectionSettings settings)
        => """
            element "59N" {
              phasor operatingVoltage = 3V0.fundamental
              pickup = abs(operatingVoltage) >= setting("Feeder.ResidualOvervoltage59NPickupV")
              dropout = abs(operatingVoltage) < setting("Feeder.ResidualOvervoltage59NPickupV")
                * setting("Feeder.ResidualOvervoltage59NDropoutRatio")
              operate = pickup.persist(setting("Feeder.ResidualOvervoltage59NDelay"))
              trip = operate && smv.allowsTrip
            }
            """;
}
