using Arvrel.Protection.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ResearchProtectionEngineTests
{
    [TestInitialize]
    public void Initialize() => AlgorithmRuntimeRegistry.ClearSession("research engine test reset");

    [TestCleanup]
    public void Cleanup() => AlgorithmRuntimeRegistry.ClearSession("research engine test cleanup");

    [TestMethod]
    public void StagedCustom_RunsSameFrameShadow_WithoutChangingStandardOutput()
    {
        var settings = new ProtectionSettings();
        var standardSource = AlgorithmSourceCatalog.Build("50P-1", settings);
        var customSource = standardSource.Replace(
            "max(IA.rms1c, IB.rms1c, IC.rms1c)",
            "IA.rms1c",
            StringComparison.Ordinal);
        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", customSource, settings, "IA-only research");
        var engine = new ResearchProtectionEngine(settings);
        var frame = new MeasurementFrame(DateTimeOffset.UnixEpoch, 1, 8, 1, 0.05, SmvTrustState.Healthy);

        var snapshot = engine.Evaluate(frame);
        var comparison = snapshot.AlgorithmComparisons.Single(item => item.Element == "50P-1");

        Assert.IsTrue(snapshot.Phase50.Pickup, "Standard max-phase element must remain authoritative while custom is shadow-only.");
        Assert.IsTrue(comparison.Standard.Pickup);
        Assert.IsFalse(comparison.Custom.Pickup);
        Assert.IsTrue(comparison.Diverged);
        Assert.IsFalse(comparison.CustomActive);
        Assert.AreEqual(staged.SourceHash, comparison.CustomSourceHash);
        Assert.AreEqual("standard-active/custom-shadow", snapshot.AlgorithmRuntime.Mode);
    }

    [TestMethod]
    public void ActivateCustom_ChangesOnlyVirtualEffectiveElement_AndKeepsStandardShadowEvidence()
    {
        var settings = new ProtectionSettings();
        var standardSource = AlgorithmSourceCatalog.Build("50P-1", settings);
        var customSource = standardSource.Replace(
            "max(IA.rms1c, IB.rms1c, IC.rms1c)",
            "IA.rms1c",
            StringComparison.Ordinal);
        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", customSource, settings, "IA-only research");
        _ = AlgorithmRuntimeRegistry.Activate("50P-1", staged.SourceHash, settings, "activate for A/B");
        var engine = new ResearchProtectionEngine(settings);
        var frame = new MeasurementFrame(DateTimeOffset.UnixEpoch, 1, 8, 1, 0.05, SmvTrustState.Healthy);

        var snapshot = engine.Evaluate(frame);
        var comparison = snapshot.AlgorithmComparisons.Single(item => item.Element == "50P-1");

        Assert.IsFalse(snapshot.Phase50.Pickup, "Activated IA-only custom element must become the effective virtual element.");
        Assert.IsTrue(comparison.Standard.Pickup, "Native standard result must remain available as shadow evidence.");
        Assert.IsFalse(comparison.Custom.Pickup);
        Assert.IsTrue(comparison.CustomActive);
        Assert.IsTrue(snapshot.AlgorithmRuntime.HasActiveCustom);
        Assert.AreEqual("custom-active/standard-shadow", snapshot.AlgorithmRuntime.Mode);
    }

    [TestMethod]
    public void ActiveCustom_CanPreventVirtualTripThatStandardReferenceWouldRequest()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(20),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        };
        var standardSource = AlgorithmSourceCatalog.Build("50P-1", settings);
        var customSource = standardSource.Replace(
            "max(IA.rms1c, IB.rms1c, IC.rms1c)",
            "IA.rms1c",
            StringComparison.Ordinal);
        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", customSource, settings);
        _ = AlgorithmRuntimeRegistry.Activate("50P-1", staged.SourceHash, settings);
        var engine = new ResearchProtectionEngine(settings);
        var start = DateTimeOffset.UnixEpoch;
        ProtectionSnapshot? snapshot = null;

        for (var index = 0; index <= 8; index++)
        {
            snapshot = engine.Evaluate(new MeasurementFrame(
                start.AddMilliseconds(index * 5),
                1,
                8,
                1,
                0.05,
                SmvTrustState.Healthy));
        }

        Assert.IsNotNull(snapshot);
        var comparison = snapshot.AlgorithmComparisons.Single(item => item.Element == "50P-1");
        Assert.IsTrue(comparison.StandardTripRequested, "Standard reference should have reached its virtual trip request.");
        Assert.IsFalse(comparison.CustomTripRequested, "IA-only custom should remain below pickup.");
        Assert.IsFalse(snapshot.TripRequested, "Activated custom output must be authoritative for its virtual element.");
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public void Rollback_RestoresNativeStandardOutputOnNextFrame()
    {
        var settings = new ProtectionSettings();
        var standardSource = AlgorithmSourceCatalog.Build("50P-1", settings);
        var customSource = standardSource.Replace(
            "max(IA.rms1c, IB.rms1c, IC.rms1c)",
            "IA.rms1c",
            StringComparison.Ordinal);
        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", customSource, settings);
        _ = AlgorithmRuntimeRegistry.Activate("50P-1", staged.SourceHash, settings);
        var engine = new ResearchProtectionEngine(settings);
        var first = engine.Evaluate(new MeasurementFrame(DateTimeOffset.UnixEpoch, 1, 8, 1, 0.05, SmvTrustState.Healthy));
        Assert.IsFalse(first.Phase50.Pickup);

        _ = AlgorithmRuntimeRegistry.RollbackElement("50P-1", "restore native standard");
        var restored = engine.Evaluate(new MeasurementFrame(DateTimeOffset.UnixEpoch.AddMilliseconds(5), 1, 8, 1, 0.05, SmvTrustState.Healthy));

        Assert.IsTrue(restored.Phase50.Pickup);
        Assert.IsFalse(restored.AlgorithmRuntime.HasActiveCustom);
        Assert.AreEqual("standard-active/custom-shadow", restored.AlgorithmRuntime.Mode);
        Assert.IsFalse(restored.AlgorithmComparisons.Single(item => item.Element == "50P-1").CustomActive);
    }

    [TestMethod]
    public void ActivatedCustom_StillCannotTripWhenSmvTrustBlocksTripPermission()
    {
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousDelay = TimeSpan.FromMilliseconds(10),
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false
        };
        var source = AlgorithmSourceCatalog.Build("50P-1", settings);
        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", source, settings);
        _ = AlgorithmRuntimeRegistry.Activate("50P-1", staged.SourceHash, settings);
        var engine = new ResearchProtectionEngine(settings);
        var trust = SmvTrustState.TripBlocked("TEST_BLOCK", "laboratory trip permission removed");
        var start = DateTimeOffset.UnixEpoch;
        ProtectionSnapshot? snapshot = null;

        for (var index = 0; index <= 4; index++)
        {
            snapshot = engine.Evaluate(new MeasurementFrame(
                start.AddMilliseconds(index * 5),
                8,
                1,
                1,
                0.05,
                trust));
        }

        Assert.IsNotNull(snapshot);
        Assert.IsFalse(snapshot.TripRequested);
        Assert.IsFalse(snapshot.TripLatched);
        Assert.IsTrue(snapshot.Blocked);
        Assert.IsFalse(snapshot.AlgorithmComparisons.Single(item => item.Element == "50P-1").CustomTripRequested);
    }
}
