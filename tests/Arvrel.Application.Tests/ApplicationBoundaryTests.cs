using Arvrel.Application.Laboratory;
using Arvrel.Application.Workspace;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class ApplicationBoundaryTests
{
    [TestMethod]
    public void DeterministicScenario_ProducesFrameworkNeutralWaveformData()
    {
        var scenario = new DeterministicLabScenario();
        scenario.StartInjection();

        var step = scenario.Advance(TimeSpan.FromMilliseconds(20));

        Assert.AreEqual(DeterministicLabScenario.WindowSamples, step.Waveform.PhaseA.Length);
        Assert.AreEqual(DeterministicLabScenario.WindowSamples, step.Waveform.PhaseB.Length);
        Assert.AreEqual(DeterministicLabScenario.WindowSamples, step.Waveform.PhaseC.Length);
        Assert.AreEqual(DeterministicLabScenario.WindowSamples, step.Waveform.Residual.Length);
        Assert.AreEqual(DeterministicLabScenario.Frequency, step.Waveform.FrequencyHz, 1e-9);
        Assert.AreEqual(DeterministicLabScenario.SamplesPerCycle, step.Waveform.SamplesPerCycle);
        Assert.IsTrue(step.Measurement.SmvTrust.AllowsMeasurement);
    }

    [TestMethod]
    public void InternalLabSession_AdvancesProtectionOnlyWhileRunning()
    {
        var session = new InternalLabSession(new ProtectionSettings());

        Assert.AreEqual(0, session.Advance(TimeSpan.FromMilliseconds(5), 8).Count);

        session.SetRunning(true);
        var ticks = session.Advance(TimeSpan.FromMilliseconds(5), 8);

        Assert.AreEqual(8, ticks.Count);
        Assert.AreSame(ticks[^1].Protection, session.Snapshot);
        Assert.AreEqual(160L, session.Scenario.SampleCounter);
        Assert.IsTrue(session.Scenario.IsRunning);
    }

    [TestMethod]
    public void InternalLabSession_ResetProtection_PreservesSourceAuthority()
    {
        var session = new InternalLabSession(new ProtectionSettings());
        session.Scenario.ApplyPreset("A-G fault");
        session.SetRunning(true);
        session.Advance(TimeSpan.FromMilliseconds(5), 8);

        session.ResetProtection();

        Assert.IsTrue(session.IsRunning);
        Assert.AreEqual("A-G fault", session.Scenario.ActiveProfile.Name);
        Assert.AreEqual("READY", session.Snapshot.ActiveElement);
        Assert.IsFalse(session.Snapshot.TripLatched);
    }

    [TestMethod]
    public void InternalLabSession_ApplySettingsPreservingSource_KeepsTestSetState()
    {
        var session = new InternalLabSession(new ProtectionSettings());
        session.ApplyProfile(VirtualInjectionPresets.Create("B-G fault", 60));
        session.Scenario.SmvDegraded = true;
        session.SetRunning(true);
        session.Advance(TimeSpan.FromMilliseconds(5), 4);

        var configuredFingerprint = session.Scenario.InjectionFingerprint;
        var outputFingerprint = session.Scenario.OutputFingerprint;
        var sampleCounter = session.Scenario.SampleCounter;
        var settings = session.Settings with
        {
            GroupName = "GROUP TEST",
            Revision = 3,
            PhaseInstantaneousPickupA = 6.5
        };

        session.ApplySettingsPreservingSource(settings);

        Assert.AreEqual("GROUP TEST", session.Settings.GroupName);
        Assert.AreEqual(3, session.Settings.Revision);
        Assert.IsTrue(session.IsRunning);
        Assert.IsTrue(session.Scenario.SmvDegraded);
        Assert.AreEqual("B-G fault", session.Scenario.ActiveProfile.Name);
        Assert.AreEqual(60, session.Scenario.ActiveProfile.FrequencyHz, 1e-9);
        Assert.AreEqual(configuredFingerprint, session.Scenario.InjectionFingerprint);
        Assert.AreEqual(outputFingerprint, session.Scenario.OutputFingerprint);
        Assert.AreEqual(sampleCounter, session.Scenario.SampleCounter);
        Assert.IsFalse(session.Snapshot.TripLatched);
        Assert.AreEqual("READY", session.Snapshot.ActiveElement);
    }

    [TestMethod]
    public void InternalLabSession_StoppedSourceRetainsConfiguredProfile()
    {
        var session = new InternalLabSession(new ProtectionSettings());
        session.ApplyProfile(VirtualInjectionPresets.Create("C-G fault"));
        var configuredFingerprint = session.Scenario.InjectionFingerprint;

        session.SetRunning(true);
        session.SetRunning(false);
        var stopped = session.Step(TimeSpan.Zero);

        Assert.IsFalse(session.IsRunning);
        Assert.AreEqual("C-G fault", session.Scenario.ActiveProfile.Name);
        Assert.AreEqual(configuredFingerprint, session.Scenario.InjectionFingerprint);
        Assert.AreEqual(0, stopped.Scenario.Measurement.PhaseA, 1e-9);
        Assert.AreEqual(0, stopped.Scenario.Measurement.PhaseB, 1e-9);
        Assert.AreEqual(0, stopped.Scenario.Measurement.PhaseC, 1e-9);
        Assert.AreEqual("stopped", session.Scenario.OutputState);
    }

    [TestMethod]
    public void Workspace_SelectingExternalSource_StopsInternalLaboratory()
    {
        var workspace = new ArvrelWorkspace(new ProtectionSettings());
        workspace.InternalLab.SetRunning(true);

        workspace.SelectSource(WorkspaceSourceMode.LiveProcessBus);
        workspace.SetExternalSourceRunning(true);

        Assert.IsFalse(workspace.InternalLab.IsRunning);
        Assert.IsTrue(workspace.ExternalSourceRunning);
        Assert.AreEqual(WorkspaceSourceMode.LiveProcessBus, workspace.SourceMode);
    }

    [TestMethod]
    public void Workspace_RejectsExternalRunInInternalMode()
    {
        var workspace = new ArvrelWorkspace(new ProtectionSettings());

        Assert.ThrowsException<InvalidOperationException>(() => workspace.SetExternalSourceRunning(true));
    }
}
