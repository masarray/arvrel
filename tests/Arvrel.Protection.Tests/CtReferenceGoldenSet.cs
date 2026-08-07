using System.Text.Json;
using System.Text.Json.Serialization;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

internal static class CtReferenceGoldenSet
{
    public static void ValidateAll()
    {
        var vectorDirectory = Path.Combine(AppContext.BaseDirectory, "validation", "vectors");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var manifest = JsonSerializer.Deserialize<VectorManifest>(
            File.ReadAllText(Path.Combine(vectorDirectory, "ct_reference_manifest.json")),
            options) ?? throw new InvalidDataException("CT reference manifest is empty.");

        Assert.AreEqual(1, manifest.SchemaVersion);
        Assert.AreEqual(6, manifest.SolverContract.Iterations);
        Assert.AreEqual(0.45, manifest.SolverContract.Relaxation, 1e-15);
        Assert.IsTrue(manifest.CaseFiles.Count >= 6);
        Assert.AreEqual(manifest.CaseFiles.Count, manifest.CaseFiles.Distinct(StringComparer.Ordinal).Count());

        foreach (var relativePath in manifest.CaseFiles)
        {
            var fullPath = Path.GetFullPath(Path.Combine(vectorDirectory, relativePath));
            Assert.IsTrue(fullPath.StartsWith(Path.GetFullPath(vectorDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            var envelope = JsonSerializer.Deserialize<VectorCaseEnvelope>(File.ReadAllText(fullPath), options)
                ?? throw new InvalidDataException($"CT reference case '{relativePath}' is empty.");
            Assert.AreEqual(1, envelope.SchemaVersion, relativePath);
            Validate(envelope.Case, manifest.AbsoluteTolerance, manifest.RelativeTolerance);
        }
    }

    private static void Validate(VectorCase vector, double absoluteTolerance, double relativeTolerance)
    {
        var ideal = GenerateSource(vector);
        var result = CtSaturationModel.Apply(
            ideal,
            vector.SampleRateHz,
            vector.FrequencyHz,
            vector.Settings.ToSettings(),
            vector.InitialState?.ToState());

        Assert.AreEqual(vector.Checkpoints.Count, vector.Expected.Ideal.Count, vector.Name);
        Assert.AreEqual(vector.Checkpoints.Count, vector.Expected.Secondary.Count, vector.Name);
        Assert.AreEqual(vector.Checkpoints.Count, vector.Expected.FluxPerUnit.Count, vector.Name);
        Assert.AreEqual(vector.Checkpoints.Count, vector.Expected.ExcitationCurrentA.Count, vector.Name);
        for (var index = 0; index < vector.Checkpoints.Count; index++)
        {
            var sample = vector.Checkpoints[index];
            Close(vector.Expected.Ideal[index], ideal[sample], absoluteTolerance, relativeTolerance, $"{vector.Name} ideal[{sample}]");
            Close(vector.Expected.Secondary[index], result.SecondaryCurrentA[sample], absoluteTolerance, relativeTolerance, $"{vector.Name} secondary[{sample}]");
            Close(vector.Expected.FluxPerUnit[index], result.FluxPerUnit[sample], absoluteTolerance, relativeTolerance, $"{vector.Name} flux[{sample}]");
            Close(vector.Expected.ExcitationCurrentA[index], result.ExcitationCurrentA[sample], absoluteTolerance, relativeTolerance, $"{vector.Name} excitation[{sample}]");
        }

        var expectedState = vector.Expected.FinalState;
        Assert.AreEqual(expectedState.Initialized, result.FinalState.Initialized, vector.Name);
        Close(expectedState.FluxLinkageVoltSeconds, result.FinalState.FluxLinkageVoltSeconds, absoluteTolerance, relativeTolerance, $"{vector.Name} final flux");
        Close(expectedState.PreviousSecondaryCurrentA, result.FinalState.PreviousSecondaryCurrentA, absoluteTolerance, relativeTolerance, $"{vector.Name} final current");
        Close(expectedState.PreviousSecondaryVoltageV, result.FinalState.PreviousSecondaryVoltageV, absoluteTolerance, relativeTolerance, $"{vector.Name} final voltage");
        Assert.AreEqual(expectedState.ProcessedSampleCount, result.FinalState.ProcessedSampleCount, vector.Name);

        var expected = vector.Expected.Diagnostics;
        var actual = result.Diagnostics;
        Assert.AreEqual(expected.Enabled, actual.Enabled, vector.Name);
        Assert.AreEqual(expected.Saturated, actual.Saturated, vector.Name);
        Assert.AreEqual(expected.SaturatedSampleCount, actual.SaturatedSampleCount, vector.Name);
        Assert.AreEqual(expected.FirstSaturatedSample, actual.FirstSaturatedSample, vector.Name);
        if (expected.FirstSaturationMilliseconds is null)
            Assert.IsTrue(double.IsNaN(actual.FirstSaturationMilliseconds), vector.Name);
        else
            Close(expected.FirstSaturationMilliseconds.Value, actual.FirstSaturationMilliseconds, absoluteTolerance, relativeTolerance, $"{vector.Name} onset ms");
        Close(expected.MaximumAbsoluteFluxPerUnit, actual.MaximumAbsoluteFluxPerUnit, absoluteTolerance, relativeTolerance, $"{vector.Name} max flux");
        Close(expected.MaximumExcitationCurrentA, actual.MaximumExcitationCurrentA, absoluteTolerance, relativeTolerance, $"{vector.Name} max excitation");
        Close(expected.MaximumSecondaryVoltageV, actual.MaximumSecondaryVoltageV, absoluteTolerance, relativeTolerance, $"{vector.Name} max voltage");
        Close(expected.IdealRmsA, actual.IdealRmsA, absoluteTolerance, relativeTolerance, $"{vector.Name} ideal RMS");
        Close(expected.SecondaryRmsA, actual.SecondaryRmsA, absoluteTolerance, relativeTolerance, $"{vector.Name} secondary RMS");
        Close(expected.RmsMagnitudeErrorPercent, actual.RmsMagnitudeErrorPercent, absoluteTolerance, relativeTolerance, $"{vector.Name} ratio error");
        Close(expected.WaveformErrorPercent, actual.WaveformErrorPercent, absoluteTolerance, relativeTolerance, $"{vector.Name} waveform error");
        Close(expected.MinimumMagnitudeRatio, actual.MinimumMagnitudeRatio, absoluteTolerance, relativeTolerance, $"{vector.Name} minimum ratio");
        Close(expected.InitialFluxPerUnit, actual.InitialFluxPerUnit, absoluteTolerance, relativeTolerance, $"{vector.Name} initial flux pu");
        Close(expected.FinalFluxPerUnit, actual.FinalFluxPerUnit, absoluteTolerance, relativeTolerance, $"{vector.Name} final flux pu");
        Assert.AreEqual(expected.StateWasCarried, actual.StateWasCarried, vector.Name);
        Assert.AreEqual(expected.InitialProcessedSampleCount, actual.InitialProcessedSampleCount, vector.Name);
        Assert.AreEqual(expected.FinalProcessedSampleCount, actual.FinalProcessedSampleCount, vector.Name);
        Assert.AreEqual(expected.FirstSaturationAbsoluteSample, actual.FirstSaturationAbsoluteSample, vector.Name);
    }

    private static double[] GenerateSource(VectorCase vector)
    {
        var source = vector.Source;
        var result = new double[source.SampleCount];
        var peak = source.RmsA * Math.Sqrt(2);
        var phase = source.PhaseDegrees * Math.PI / 180;
        var dcFraction = source.DcOffsetPercent / 100d;
        var timeConstant = source.DcTimeConstantMilliseconds / 1_000d;
        for (var index = 0; index < result.Length; index++)
        {
            var absoluteSample = checked(source.StartSampleIndex + index);
            var time = absoluteSample / vector.SampleRateHz;
            result[index] = peak * Math.Cos(2 * Math.PI * vector.FrequencyHz * time + phase) +
                peak * dcFraction * Math.Exp(-time / timeConstant);
        }
        return result;
    }

    private static void Close(double expected, double actual, double absoluteTolerance, double relativeTolerance, string message)
    {
        var tolerance = absoluteTolerance + relativeTolerance * Math.Max(Math.Abs(expected), Math.Abs(actual));
        Assert.IsTrue(
            Math.Abs(expected - actual) <= tolerance,
            $"{message}: expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}");
    }
}
