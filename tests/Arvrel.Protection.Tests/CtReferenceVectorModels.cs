using Arvrel.Protection;

namespace Arvrel.Protection.Tests;

internal sealed record VectorManifest(
    int SchemaVersion,
    SolverContract SolverContract,
    double AbsoluteTolerance,
    double RelativeTolerance,
    IReadOnlyList<string> CaseFiles);

internal sealed record VectorCaseEnvelope(int SchemaVersion, VectorCase Case);

internal sealed record SolverContract(int Iterations, double Relaxation);

internal sealed record VectorCase(
    string Name,
    double SampleRateHz,
    double FrequencyHz,
    SourceSpec Source,
    SettingsSpec Settings,
    StateSpec? InitialState,
    IReadOnlyList<int> Checkpoints,
    ExpectedSpec Expected);

internal sealed record SourceSpec(
    double RmsA,
    double PhaseDegrees,
    double DcOffsetPercent,
    double DcTimeConstantMilliseconds,
    int StartSampleIndex,
    int SampleCount);

internal sealed record SettingsSpec(
    bool Enabled,
    double RatedSecondaryCurrentA,
    double KneePointVoltageRms,
    double SecondaryWindingResistanceOhm,
    double BurdenResistanceOhm,
    double BurdenInductanceMilliHenries,
    double ExcitationCurrentAtKneeA,
    double ExcitationCurrentAtTwiceKneeA,
    double SaturationExponent,
    double RemanencePercent,
    double MaximumFluxPerUnit,
    double MaximumExcitationCurrentA)
{
    public CtSaturationSettings ToSettings() => new()
    {
        Enabled = Enabled,
        RatedSecondaryCurrentA = RatedSecondaryCurrentA,
        KneePointVoltageRms = KneePointVoltageRms,
        SecondaryWindingResistanceOhm = SecondaryWindingResistanceOhm,
        BurdenResistanceOhm = BurdenResistanceOhm,
        BurdenInductanceMilliHenries = BurdenInductanceMilliHenries,
        ExcitationCurrentAtKneeA = ExcitationCurrentAtKneeA,
        ExcitationCurrentAtTwiceKneeA = ExcitationCurrentAtTwiceKneeA,
        SaturationExponent = SaturationExponent,
        RemanencePercent = RemanencePercent,
        MaximumFluxPerUnit = MaximumFluxPerUnit,
        MaximumExcitationCurrentA = MaximumExcitationCurrentA
    };
}

internal sealed record StateSpec(
    bool Initialized,
    double FluxLinkageVoltSeconds,
    double PreviousSecondaryCurrentA,
    double PreviousSecondaryVoltageV,
    long ProcessedSampleCount)
{
    public CtSaturationChannelState ToState() => new(
        Initialized,
        FluxLinkageVoltSeconds,
        PreviousSecondaryCurrentA,
        PreviousSecondaryVoltageV,
        ProcessedSampleCount);
}

internal sealed record ExpectedSpec(
    IReadOnlyList<double> Ideal,
    IReadOnlyList<double> Secondary,
    IReadOnlyList<double> FluxPerUnit,
    IReadOnlyList<double> ExcitationCurrentA,
    StateSpec FinalState,
    DiagnosticsSpec Diagnostics);

internal sealed record DiagnosticsSpec(
    bool Enabled,
    bool Saturated,
    int SaturatedSampleCount,
    int FirstSaturatedSample,
    double? FirstSaturationMilliseconds,
    double MaximumAbsoluteFluxPerUnit,
    double MaximumExcitationCurrentA,
    double MaximumSecondaryVoltageV,
    double IdealRmsA,
    double SecondaryRmsA,
    double RmsMagnitudeErrorPercent,
    double WaveformErrorPercent,
    double MinimumMagnitudeRatio,
    double InitialFluxPerUnit,
    double FinalFluxPerUnit,
    bool StateWasCarried,
    long InitialProcessedSampleCount,
    long FinalProcessedSampleCount,
    long FirstSaturationAbsoluteSample);
