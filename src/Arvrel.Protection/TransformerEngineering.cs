using System.Text.RegularExpressions;

namespace Arvrel.Protection;

public enum TransformerWindingConnection
{
    Wye,
    Delta,
    ZigZag
}

public sealed record TransformerCtRatio(double PrimaryA, double SecondaryA)
{
    public double Ratio => PrimaryA / SecondaryA;

    public void Validate(string name)
    {
        if (!double.IsFinite(PrimaryA) || PrimaryA <= 0)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(PrimaryA)}");
        if (!double.IsFinite(SecondaryA) || SecondaryA <= 0)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(SecondaryA)}");
    }
}

public sealed record TransformerWindingEngineering(
    TransformerCtRatio PhaseCt,
    TransformerCtRatio? NeutralCt = null)
{
    /// <summary>
    /// Set only when the imported current polarity is opposite the ARVREL zone-current
    /// convention. The normal convention is positive current into the protected transformer.
    /// </summary>
    public bool ReversePhasePolarity { get; init; }

    /// <summary>Additional neutral-CT polarity reversal relative to the phase CT reference.</summary>
    public bool ReverseNeutralPolarity { get; init; }

    public void Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(PhaseCt);
        PhaseCt.Validate($"{name}.{nameof(PhaseCt)}");
        NeutralCt?.Validate($"{name}.{nameof(NeutralCt)}");
    }

    public double NeutralToPhaseCtScale => NeutralCt is null ? 1 : NeutralCt.Ratio / PhaseCt.Ratio;
}

public sealed record TransformerNameplate(
    double RatedPowerMva,
    double HighVoltageKv,
    double LowVoltageKv,
    string VectorGroup)
{
    public void Validate()
    {
        if (!double.IsFinite(RatedPowerMva) || RatedPowerMva <= 0)
            throw new ArgumentOutOfRangeException(nameof(RatedPowerMva));
        if (!double.IsFinite(HighVoltageKv) || HighVoltageKv <= 0)
            throw new ArgumentOutOfRangeException(nameof(HighVoltageKv));
        if (!double.IsFinite(LowVoltageKv) || LowVoltageKv <= 0)
            throw new ArgumentOutOfRangeException(nameof(LowVoltageKv));
        if (string.IsNullOrWhiteSpace(VectorGroup))
            throw new ArgumentException("Transformer vector group is required.", nameof(VectorGroup));
    }
}

public sealed record TransformerVectorGroup(
    string Name,
    TransformerWindingConnection HighVoltageConnection,
    bool HighVoltageNeutralBroughtOut,
    TransformerWindingConnection LowVoltageConnection,
    bool LowVoltageNeutralBroughtOut,
    int ClockNumber)
{
    private static readonly Regex VectorGroupPattern = new(
        "^(?<hv>YN|Y|D|ZN|Z)(?<lv>yn|y|d|zn|z)(?<clock>0|[1-9]|1[01])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Signed IEC clock displacement. Positive means the LV reference lags the HV
    /// reference; 11 therefore becomes -30 degrees (LV leads HV by 30 degrees).
    /// </summary>
    public double SignedClockDisplacementDegrees => NormalizeDegrees(ClockNumber * 30.0);

    public bool AutomaticCurrentCompensationSupported =>
        HighVoltageConnection != TransformerWindingConnection.ZigZag &&
        LowVoltageConnection != TransformerWindingConnection.ZigZag;

    public static TransformerVectorGroup Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = text.Trim();
        var match = VectorGroupPattern.Match(normalized);
        if (!match.Success)
        {
            throw new FormatException(
                $"Unsupported two-winding vector group '{text}'. Expected forms such as Dyn11, YNd1, Yyn0 or Dd0.");
        }

        var hvToken = match.Groups["hv"].Value;
        var lvToken = match.Groups["lv"].Value;
        var clock = int.Parse(match.Groups["clock"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return new TransformerVectorGroup(
            normalized,
            ParseConnection(hvToken),
            hvToken.EndsWith('N'),
            ParseConnection(lvToken),
            lvToken.EndsWith('n'),
            clock);
    }

    private static TransformerWindingConnection ParseConnection(string token) => char.ToUpperInvariant(token[0]) switch
    {
        'Y' => TransformerWindingConnection.Wye,
        'D' => TransformerWindingConnection.Delta,
        'Z' => TransformerWindingConnection.ZigZag,
        _ => throw new FormatException($"Unsupported winding connection '{token}'.")
    };

    private static double NormalizeDegrees(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle <= -180) angle += 360;
        return angle;
    }
}

public sealed record TransformerEngineeringPlan(
    TransformerNameplate Nameplate,
    TransformerVectorGroup VectorGroup,
    TransformerWindingEngineering HighVoltage,
    TransformerWindingEngineering LowVoltage,
    double HighVoltageRatedCurrentA,
    double LowVoltageRatedCurrentA,
    TransformerWindingCompensation HighVoltageCompensation,
    TransformerWindingCompensation LowVoltageCompensation)
{
    public string Summary =>
        $"{Nameplate.RatedPowerMva:0.###} MVA · {Nameplate.HighVoltageKv:0.###}/{Nameplate.LowVoltageKv:0.###} kV · " +
        $"{VectorGroup.Name} · Ibase {HighVoltageRatedCurrentA:0.###}/{LowVoltageRatedCurrentA:0.###} A";

    public TransformerProtectionSettings ApplyTo(TransformerProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        return settings with
        {
            Differential87T = settings.Differential87T with
            {
                HighVoltageCompensation = HighVoltageCompensation,
                LowVoltageCompensation = LowVoltageCompensation
            },
            RefHighVoltage = settings.RefHighVoltage with
            {
                NeutralCurrentScale = HighVoltage.NeutralToPhaseCtScale,
                ReverseNeutralPolarity = HighVoltage.ReverseNeutralPolarity
            },
            RefLowVoltage = settings.RefLowVoltage with
            {
                NeutralCurrentScale = LowVoltage.NeutralToPhaseCtScale,
                ReverseNeutralPolarity = LowVoltage.ReverseNeutralPolarity
            }
        };
    }
}

public static class TransformerEngineeringAdapter
{
    /// <summary>
    /// Builds a deterministic current-domain compensation plan from transformer
    /// nameplate and CT data. Source phasors are assumed to be CT-secondary amperes.
    /// Phase CT polarity should follow the protection-zone convention: positive into
    /// the transformer on both winding sides.
    /// </summary>
    public static TransformerEngineeringPlan Build(
        TransformerNameplate nameplate,
        TransformerWindingEngineering highVoltage,
        TransformerWindingEngineering lowVoltage)
    {
        ArgumentNullException.ThrowIfNull(nameplate);
        ArgumentNullException.ThrowIfNull(highVoltage);
        ArgumentNullException.ThrowIfNull(lowVoltage);
        nameplate.Validate();
        highVoltage.Validate(nameof(highVoltage));
        lowVoltage.Validate(nameof(lowVoltage));

        var vectorGroup = TransformerVectorGroup.Parse(nameplate.VectorGroup);
        if (!vectorGroup.AutomaticCurrentCompensationSupported)
        {
            throw new NotSupportedException(
                $"Automatic current compensation for vector group {vectorGroup.Name} is not implemented yet. " +
                "Use explicit TransformerWindingCompensation for zig-zag applications.");
        }

        var highVoltageRatedCurrent = RatedCurrent(nameplate.RatedPowerMva, nameplate.HighVoltageKv);
        var lowVoltageRatedCurrent = RatedCurrent(nameplate.RatedPowerMva, nameplate.LowVoltageKv);

        var highVoltageZeroSequenceRemoval =
            vectorGroup.HighVoltageConnection != TransformerWindingConnection.Delta &&
            vectorGroup.LowVoltageConnection == TransformerWindingConnection.Delta;
        var lowVoltageZeroSequenceRemoval =
            vectorGroup.LowVoltageConnection != TransformerWindingConnection.Delta &&
            vectorGroup.HighVoltageConnection == TransformerWindingConnection.Delta;

        var highVoltageCompensation = new TransformerWindingCompensation
        {
            CurrentScaleToPu = highVoltage.PhaseCt.Ratio / highVoltageRatedCurrent,
            PhaseShiftDegrees = 0,
            ReversePolarity = highVoltage.ReversePhasePolarity,
            RemoveZeroSequence = highVoltageZeroSequenceRemoval
        };
        var lowVoltageCompensation = new TransformerWindingCompensation
        {
            CurrentScaleToPu = lowVoltage.PhaseCt.Ratio / lowVoltageRatedCurrent,
            PhaseShiftDegrees = vectorGroup.SignedClockDisplacementDegrees,
            ReversePolarity = lowVoltage.ReversePhasePolarity,
            RemoveZeroSequence = lowVoltageZeroSequenceRemoval
        };

        highVoltageCompensation.Validate(nameof(highVoltageCompensation));
        lowVoltageCompensation.Validate(nameof(lowVoltageCompensation));

        return new TransformerEngineeringPlan(
            nameplate,
            vectorGroup,
            highVoltage,
            lowVoltage,
            highVoltageRatedCurrent,
            lowVoltageRatedCurrent,
            highVoltageCompensation,
            lowVoltageCompensation);
    }

    private static double RatedCurrent(double ratedPowerMva, double ratedVoltageKv)
        => ratedPowerMva * 1_000_000 / (Math.Sqrt(3) * ratedVoltageKv * 1_000);
}
