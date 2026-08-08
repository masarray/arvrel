using Arvrel.Protection.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class AlgorithmSandboxRuntimeTests
{
    [TestMethod]
    [DataRow("50P-1")]
    [DataRow("51P")]
    [DataRow("50N")]
    [DataRow("51N")]
    public void GeneratedLegacyElements_CompileAndExecuteInSandbox(string element)
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build(element, settings);

        var compiled = AlgorithmSandboxCompiler.Compile(element, source, settings);
        var instance = compiled.CreateInstance();
        var frame = new MeasurementFrame(DateTimeOffset.UnixEpoch, 8, 1, 1, 2, SmvTrustState.Healthy);
        var result = instance.Evaluate(frame, settings);

        Assert.AreEqual(element, compiled.Element);
        Assert.AreEqual(64, compiled.SourceHash.Length);
        Assert.IsTrue(compiled.InstructionCost <= AlgorithmSandboxCompiler.MaximumInstructionsPerFrame);
        Assert.AreEqual(element is "50N" or "51N" ? 2d : 8d, result.Snapshot.OperatingQuantity, 1e-9);
    }

    [TestMethod]
    [DataRow("50P-1")]
    [DataRow("51P")]
    [DataRow("50N")]
    [DataRow("51N")]
    public void IdenticalSource_TestBenchHasNoBehaviorDivergence(string element)
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build(element, settings);

        var result = AlgorithmTestBench.Compare(element, source, source, settings);

        Assert.IsTrue(result.Frames > 100);
        Assert.AreEqual(0, result.DivergentFrames);
        Assert.AreEqual(result.StandardFirstPickup, result.CustomFirstPickup);
        Assert.AreEqual(result.StandardFirstTrip, result.CustomFirstTrip);
    }

    [TestMethod]
    public void PhaseInstantaneous_TripsOnlyAfterPersistDelay()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousPickupA = 4,
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(60)
        };
        var compiled = AlgorithmSandboxCompiler.Compile("50P-1", AlgorithmSourceCatalog.Build("50P-1", settings), settings);
        var instance = compiled.CreateInstance();
        var start = DateTimeOffset.UnixEpoch;
        AlgorithmExecutionResult? result = null;

        for (var index = 0; index <= 12; index++)
        {
            result = instance.Evaluate(
                new MeasurementFrame(start.AddMilliseconds(index * 5), 8, 1, 1, 0.1, SmvTrustState.Healthy),
                settings);
            if (index < 12)
                Assert.IsFalse(result.TripRequested, $"Trip occurred too early at {index * 5} ms.");
        }

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TripRequested);
        Assert.IsTrue(result.OperationReached);
    }

    [TestMethod]
    public void HostSmvBoundary_BlocksTripEvenWhenOperationReached()
    {
        var settings = new ProtectionSettings { PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(10) };
        var compiled = AlgorithmSandboxCompiler.Compile("50P-1", AlgorithmSourceCatalog.Build("50P-1", settings), settings);
        var instance = compiled.CreateInstance();
        var start = DateTimeOffset.UnixEpoch;
        AlgorithmExecutionResult? result = null;

        for (var index = 0; index <= 4; index++)
        {
            result = instance.Evaluate(
                new MeasurementFrame(
                    start.AddMilliseconds(index * 5),
                    8,
                    1,
                    1,
                    0.1,
                    SmvTrustState.TripBlocked("TEST", "Deliberate laboratory trust block.")),
                settings);
        }

        Assert.IsNotNull(result);
        Assert.IsTrue(result.OperationReached);
        Assert.IsFalse(result.TripRequested);
        Assert.AreEqual(ProtectionStageState.Blocked, result.Snapshot.State);
    }

    [TestMethod]
    public void CustomMeasurementLogic_ProducesDeterministicAbDivergence()
    {
        var settings = new ProtectionSettings();
        var standard = AlgorithmSourceCatalog.Build("50P-1", settings);
        var custom = standard.Replace(
            "max(IA.rms1c, IB.rms1c, IC.rms1c)",
            "IA.rms1c",
            StringComparison.Ordinal);
        var compiled = AlgorithmSandboxCompiler.Compile("50P-1", custom, settings);
        var instance = compiled.CreateInstance();
        var start = DateTimeOffset.UnixEpoch;

        var result = instance.Evaluate(
            new MeasurementFrame(start, 1, 8, 1, 0.1, SmvTrustState.Healthy),
            settings);

        Assert.IsFalse(result.Snapshot.Pickup);
        Assert.AreEqual(1d, result.Snapshot.OperatingQuantity, 1e-9);
    }

    [TestMethod]
    public void WrongUnitForPersistDuration_FailsCompileBeforeStaging()
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("50P-1", settings).Replace(
            "setting(\"PhaseInstantaneousDelay\")",
            "setting(\"PhaseInstantaneousPickupA\")",
            StringComparison.Ordinal);

        var exception = Assert.ThrowsExactly<AlgorithmCompilationException>(
            () => AlgorithmSandboxCompiler.Compile("50P-1", source, settings));

        StringAssert.Contains(exception.Message, "duration");
    }

    [TestMethod]
    public void UnknownSetting_FailsCompileBeforeStaging()
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("50P-1", settings).Replace(
            "PhaseInstantaneousPickupA",
            "NotExposedSetting",
            StringComparison.Ordinal);

        var exception = Assert.ThrowsExactly<AlgorithmCompilationException>(
            () => AlgorithmSandboxCompiler.Compile("50P-1", source, settings));

        StringAssert.Contains(exception.Message, "Unknown or non-exposed setting");
    }

    [TestMethod]
    public void FeederElement_RemainsShadowOnlyAtRuntimeBoundary()
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("67P", settings);

        var exception = Assert.ThrowsExactly<AlgorithmCompilationException>(
            () => AlgorithmSandboxCompiler.Compile("67P", source, settings));

        StringAssert.Contains(exception.Message, "restricted to 50P-1, 51P, 50N and 51N");
    }

    [TestMethod]
    public void OversizedSource_IsRejectedByMemoryLimit()
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("50P-1", settings) + new string(' ', AlgorithmSandboxCompiler.MaximumSourceBytes + 1);

        var exception = Assert.ThrowsExactly<AlgorithmCompilationException>(
            () => AlgorithmSandboxCompiler.Compile("50P-1", source, settings));

        StringAssert.Contains(exception.Message, "memory limit");
    }
}
