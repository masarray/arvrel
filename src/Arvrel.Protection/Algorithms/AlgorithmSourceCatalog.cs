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
}
