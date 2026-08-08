using Arvrel.Protection.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AlgorithmRuntimeRegistryTests
{
    [TestInitialize]
    public void Initialize() => AlgorithmRuntimeRegistry.ClearSession("test reset");

    [TestCleanup]
    public void Cleanup() => AlgorithmRuntimeRegistry.ClearSession("test cleanup");

    [TestMethod]
    public void Stage_DoesNotActivateDefinition()
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("50P-1", settings);

        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", source, settings, "stage-only test");
        var snapshot = AlgorithmRuntimeRegistry.Snapshot();

        Assert.AreEqual("virtual-only", staged.OutputBoundary);
        Assert.AreEqual(settings.Fingerprint(), staged.SettingsFingerprint);
        Assert.IsFalse(AlgorithmRuntimeRegistry.IsActive("50P-1"));
        Assert.AreEqual("standard-active/custom-shadow", snapshot.Mode);
        Assert.IsTrue(snapshot.Staged.Any(item => item.Element == "50P-1" && item.SourceHash == staged.SourceHash));
        Assert.IsFalse(snapshot.Active.Any(item => item.Element == "50P-1"));
        Assert.IsTrue(snapshot.AuditTrail.Any(item => item.Action == "STAGE" && item.SourceHash == staged.SourceHash));
    }

    [TestMethod]
    public void Activate_RequiresExactStagedHash_AndMarksOnlyThatDefinitionActive()
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("50P-1", settings);
        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", source, settings, "candidate A");

        var activated = AlgorithmRuntimeRegistry.Activate("50P-1", staged.SourceHash, settings, "approved in laboratory");
        var snapshot = AlgorithmRuntimeRegistry.Snapshot();

        Assert.IsTrue(AlgorithmRuntimeRegistry.IsActive("50P-1", staged.SourceHash));
        Assert.AreEqual(staged.SourceHash, activated.SourceHash);
        Assert.IsNotNull(activated.ActivatedUtc);
        Assert.AreEqual("approved in laboratory", activated.AuthorNote);
        Assert.AreEqual("custom-active/standard-shadow", snapshot.Mode);
        Assert.AreEqual(1, snapshot.Active.Count(item => item.Element == "50P-1"));
        Assert.IsTrue(snapshot.AuditTrail.Any(item => item.Action == "ACTIVATE" && item.SourceHash == staged.SourceHash));
    }

    [TestMethod]
    public void Activate_WithDifferentHash_IsRejected()
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("50P-1", settings);
        _ = AlgorithmRuntimeRegistry.Stage("50P-1", source, settings);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => AlgorithmRuntimeRegistry.Activate("50P-1", new string('0', 64), settings));

        StringAssert.Contains(exception.Message, "exact validated staged definition");
        Assert.IsFalse(AlgorithmRuntimeRegistry.IsActive("50P-1"));
    }

    [TestMethod]
    public void Activate_AfterSettingsChange_IsRejectedUntilRestaged()
    {
        var stagedSettings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("50P-1", stagedSettings);
        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", source, stagedSettings);
        var changedSettings = stagedSettings with
        {
            Revision = stagedSettings.Revision + 1,
            PhaseInstantaneousPickupA = stagedSettings.PhaseInstantaneousPickupA + 0.25
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => AlgorithmRuntimeRegistry.Activate("50P-1", staged.SourceHash, changedSettings));

        StringAssert.Contains(exception.Message, "settings changed after staging");
        Assert.IsFalse(AlgorithmRuntimeRegistry.IsActive("50P-1"));
        Assert.AreNotEqual(staged.SettingsFingerprint, changedSettings.Fingerprint());
    }

    [TestMethod]
    public void Rollback_FromFirstCustom_ReturnsToStandardRuntime()
    {
        var settings = new ProtectionSettings();
        var source = AlgorithmSourceCatalog.Build("50P-1", settings);
        var staged = AlgorithmRuntimeRegistry.Stage("50P-1", source, settings);
        _ = AlgorithmRuntimeRegistry.Activate("50P-1", staged.SourceHash, settings);

        var restored = AlgorithmRuntimeRegistry.RollbackElement("50P-1", "restore standard");
        var snapshot = AlgorithmRuntimeRegistry.Snapshot();

        Assert.IsNull(restored);
        Assert.IsFalse(AlgorithmRuntimeRegistry.IsActive("50P-1"));
        Assert.AreEqual("standard-active/custom-shadow", snapshot.Mode);
        Assert.IsTrue(snapshot.AuditTrail.Any(item => item.Action == "ROLLBACK" && item.Element == "50P-1"));
    }

    [TestMethod]
    public void Rollback_RestoresPreviousCustomDefinition()
    {
        var settings = new ProtectionSettings();
        var standard = AlgorithmSourceCatalog.Build("50P-1", settings);
        var firstSource = standard.Replace("IA.rms1c", "IB.rms1c", StringComparison.Ordinal);
        var secondSource = standard.Replace("IA.rms1c", "IC.rms1c", StringComparison.Ordinal);

        var first = AlgorithmRuntimeRegistry.Stage("50P-1", firstSource, settings, "first");
        _ = AlgorithmRuntimeRegistry.Activate("50P-1", first.SourceHash, settings);
        var second = AlgorithmRuntimeRegistry.Stage("50P-1", secondSource, settings, "second");
        _ = AlgorithmRuntimeRegistry.Activate("50P-1", second.SourceHash, settings);

        var restored = AlgorithmRuntimeRegistry.RollbackElement("50P-1", "go back one version");

        Assert.IsNotNull(restored);
        Assert.AreEqual(first.SourceHash, restored.SourceHash);
        Assert.IsTrue(AlgorithmRuntimeRegistry.IsActive("50P-1", first.SourceHash));
        Assert.IsFalse(AlgorithmRuntimeRegistry.IsActive("50P-1", second.SourceHash));
    }

    [TestMethod]
    public void VersionHashAndAuthorNote_ArePresentInEvidenceSnapshot()
    {
        var settings = new ProtectionSettings();
        var staged = AlgorithmRuntimeRegistry.Stage(
            "51P",
            AlgorithmSourceCatalog.Build("51P", settings),
            settings,
            "curve research candidate");
        _ = AlgorithmRuntimeRegistry.Activate("51P", staged.SourceHash, settings, "bench A/B accepted");

        var active = AlgorithmRuntimeRegistry.Snapshot().Active.Single(item => item.Element == "51P");

        Assert.IsTrue(active.Version.StartsWith("custom-", StringComparison.Ordinal));
        Assert.AreEqual(64, active.SourceHash.Length);
        Assert.AreEqual("bench A/B accepted", active.AuthorNote);
        Assert.AreEqual("virtual-only", active.OutputBoundary);
    }
}
