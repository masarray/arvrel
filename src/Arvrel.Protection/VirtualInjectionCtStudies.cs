namespace Arvrel.Protection;

/// <summary>
/// Operator-facing CT model starting points. These are deliberately independent from
/// the virtual source/fault preset so the same source condition can be studied with
/// ideal, nominal, high-burden, and remanent CT behaviour.
/// </summary>
public static class VirtualInjectionCtModelPresets
{
    public const string Ideal = "Ideal (CT off)";
    public const string Nominal = "Protection CT · nominal";
    public const string HighBurden = "High burden · rem 0%";
    public const string HighBurdenPositiveRemanence = "High burden · rem +60%";
    public const string HighBurdenNegativeRemanence = "High burden · rem -60%";

    public static IReadOnlyList<string> Names { get; } =
    [
        Ideal,
        Nominal,
        HighBurden,
        HighBurdenPositiveRemanence,
        HighBurdenNegativeRemanence
    ];

    public static CtSaturationSettings Create(string name) => name switch
    {
        Ideal => CtSaturationSettings.Disabled,
        Nominal => CtSaturationSettings.ProtectionClass5AStudy,
        HighBurden => HighBurdenSettings(0),
        HighBurdenPositiveRemanence => HighBurdenSettings(60),
        HighBurdenNegativeRemanence => HighBurdenSettings(-60),
        _ => throw new ArgumentException($"Unknown CT model preset '{name}'.", nameof(name))
    };

    public static string? ResolveName(CtSaturationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Enabled)
            return Ideal;

        foreach (var name in Names.Skip(1))
        {
            if (settings == Create(name))
                return name;
        }
        return null;
    }

    public static CtSaturationSettings HighBurdenSettings(double remanencePercent)
        => new()
        {
            Enabled = true,
            RatedSecondaryCurrentA = 5,
            KneePointVoltageRms = 70,
            SecondaryWindingResistanceOhm = 1,
            BurdenResistanceOhm = 3.5,
            BurdenInductanceMilliHenries = 0.2,
            ExcitationCurrentAtKneeA = 0.03,
            ExcitationCurrentAtTwiceKneeA = 50,
            SaturationExponent = 2,
            RemanencePercent = remanencePercent,
            MaximumFluxPerUnit = 2.5,
            MaximumExcitationCurrentA = 200
        };
}

/// <summary>
/// Convenience source scenarios for quickly demonstrating nonlinear CT behaviour.
/// The CT model itself remains independently selectable through
/// <see cref="VirtualInjectionCtModelPresets"/>.
/// </summary>
public static class VirtualInjectionCtStudyScenarios
{
    public const string PhaseAAsymmetrical = "CT saturation - A-G asymmetrical";
    public const string PhaseBAsymmetrical = "CT saturation - B-G asymmetrical";
    public const string PhaseCAsymmetrical = "CT saturation - C-G asymmetrical";
    public const string ThreePhaseHighBurden = "CT saturation - 3-phase high burden";

    public static IReadOnlyList<string> AdditionalNames { get; } =
    [
        PhaseBAsymmetrical,
        PhaseCAsymmetrical,
        ThreePhaseHighBurden
    ];

    public static bool Contains(string name)
        => string.Equals(name, PhaseAAsymmetrical, StringComparison.Ordinal) ||
           AdditionalNames.Contains(name, StringComparer.Ordinal);

    public static VirtualInjectionProfile Create(string name, double frequencyHz = 50)
    {
        if (string.Equals(name, PhaseAAsymmetrical, StringComparison.Ordinal))
            return VirtualInjectionPresets.Create(name, frequencyHz);

        var ct = VirtualInjectionCtModelPresets.Create(
            VirtualInjectionCtModelPresets.HighBurdenPositiveRemanence);
        return name switch
        {
            PhaseBAsymmetrical => GroundFault(
                name,
                frequencyHz,
                phase: 1,
                voltageAngle: -120,
                currentAngle: -120,
                ct),
            PhaseCAsymmetrical => GroundFault(
                name,
                frequencyHz,
                phase: 2,
                voltageAngle: 120,
                currentAngle: 120,
                ct),
            ThreePhaseHighBurden => ThreePhase(name, frequencyHz, ct),
            _ => throw new ArgumentException($"Unknown CT saturation study scenario '{name}'.", nameof(name))
        };
    }

    private static VirtualInjectionProfile GroundFault(
        string name,
        double frequencyHz,
        int phase,
        double voltageAngle,
        double currentAngle,
        CtSaturationSettings ct)
    {
        var profile = VirtualInjectionPresets.Create(
            phase switch
            {
                1 => "B-G fault",
                2 => "C-G fault",
                _ => "A-G fault"
            },
            frequencyHz) with
        {
            Name = name,
            CurrentTransformer = ct
        };
        var current = new VirtualInjectionChannel(true, 20, currentAngle)
        {
            DcOffsetPercent = 100,
            DcTimeConstantMilliseconds = 60
        };
        var voltage = new VirtualInjectionChannel(true, 12, voltageAngle);
        return phase switch
        {
            1 => (profile with { PhaseBVoltage = voltage, PhaseBCurrent = current }).Normalize(),
            2 => (profile with { PhaseCVoltage = voltage, PhaseCCurrent = current }).Normalize(),
            _ => (profile with { PhaseAVoltage = voltage, PhaseACurrent = current }).Normalize()
        };
    }

    private static VirtualInjectionProfile ThreePhase(
        string name,
        double frequencyHz,
        CtSaturationSettings ct)
        => (VirtualInjectionPresets.Create("Three-phase fault", frequencyHz) with
        {
            Name = name,
            PhaseACurrent = new VirtualInjectionChannel(true, 20, 0),
            PhaseBCurrent = new VirtualInjectionChannel(true, 20, -120),
            PhaseCCurrent = new VirtualInjectionChannel(true, 20, 120),
            CurrentTransformer = ct
        }).Normalize();
}
