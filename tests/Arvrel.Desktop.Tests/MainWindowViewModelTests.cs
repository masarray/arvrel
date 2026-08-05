using Arvrel.Desktop.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public async Task StartsWithPortableInternalVirtualTestSetReady()
    {
        await using var viewModel = new MainWindowViewModel();

        Assert.AreEqual("INTERNAL VIRTUAL TEST SET", viewModel.SourceModeText);
        Assert.IsFalse(viewModel.IsRunning);
        Assert.IsFalse(viewModel.FaultActive);
        Assert.IsFalse(viewModel.SmvDegraded);
        Assert.AreEqual("START INJECTION", viewModel.RunButtonText);
        Assert.AreEqual("Normal balanced", viewModel.ProfileNameText);
        Assert.AreEqual(14, viewModel.PresetNames.Count);
        Assert.AreEqual(8, viewModel.InjectionChannels.Count);
        Assert.IsNotNull(viewModel.Waveform);
        Assert.AreEqual(160, viewModel.Waveform.PhaseA.Length);
        Assert.AreEqual(4, viewModel.ProtectionElements.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.LiveCaptureStatus));
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.ReplayStatus));
    }

    [TestMethod]
    public async Task RunTickAdvancesThePortableApplicationCoreAndStopRetainsConfiguredSource()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.SelectedPreset = "B-G fault";
        var configuredFingerprint = viewModel.InjectionFingerprintText;
        var initialCounter = viewModel.SampleCounterText;

        viewModel.ToggleRun();
        viewModel.Tick();

        Assert.IsTrue(viewModel.IsRunning);
        Assert.AreEqual("STOP INJECTION", viewModel.RunButtonText);
        Assert.AreNotEqual(initialCounter, viewModel.SampleCounterText);

        viewModel.ToggleRun();

        Assert.IsFalse(viewModel.IsRunning);
        Assert.AreEqual("B-G fault", viewModel.ProfileNameText);
        Assert.AreEqual(configuredFingerprint, viewModel.InjectionFingerprintText);
        Assert.AreEqual("STOPPED", viewModel.OutputStateText);
    }

    [TestMethod]
    public async Task PresetSelectionAndFrequencyPopulateTheEditableSource()
    {
        await using var viewModel = new MainWindowViewModel();

        viewModel.FrequencyText = "60";
        viewModel.SelectedPreset = "C-G fault";

        Assert.AreEqual("C-G fault", viewModel.ProfileNameText);
        Assert.AreEqual("60", viewModel.FrequencyText);
        Assert.AreEqual("60.000 Hz", viewModel.FrequencyTextDisplay);
        Assert.AreEqual("C-G fault", viewModel.SelectedPreset);
        Assert.IsTrue(viewModel.InjectionChannels.Any(channel =>
            channel.SignalLabel == "IC" && channel.Enabled));
    }

    [TestMethod]
    public async Task InvalidInjectionDraftLeavesLastValidSourceActiveAndBlocksStart()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.SelectedPreset = "A-B fault";
        var fingerprint = viewModel.InjectionFingerprintText;

        viewModel.FrequencyText = "90";
        var applied = viewModel.TryApplyInjection(announce: true);
        viewModel.ToggleRun();

        Assert.IsFalse(applied);
        Assert.IsFalse(viewModel.IsRunning);
        Assert.AreEqual("A-B fault", viewModel.ProfileNameText);
        Assert.AreEqual(fingerprint, viewModel.InjectionFingerprintText);
        StringAssert.Contains(viewModel.InjectionEditorStatus, "INVALID");
    }

    [TestMethod]
    public async Task FaultAndSmvDegradationReachProtectionAndTrustState()
    {
        await using var viewModel = new MainWindowViewModel();

        viewModel.InjectAgFault();
        viewModel.Tick();
        viewModel.Tick();
        viewModel.Tick();

        Assert.IsTrue(viewModel.FaultActive);
        Assert.IsTrue(viewModel.IsRunning);
        Assert.IsTrue(viewModel.PickupActive || viewModel.TripLatched);

        viewModel.ToggleSmvDegradation();
        viewModel.Tick();

        Assert.IsTrue(viewModel.SmvDegraded);
        Assert.IsFalse(viewModel.AllowsTrip);
        StringAssert.Contains(viewModel.TrustStateText, "TRIP BLOCKED");
    }

    [TestMethod]
    public async Task ClearInjectionDoesNotClearLatchedTrip()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.InjectAgFault();
        for (var index = 0; index < 5; index++)
            viewModel.Tick();

        Assert.IsTrue(viewModel.TripLatched);

        viewModel.ClearInjection();

        Assert.AreEqual("Normal balanced", viewModel.ProfileNameText);
        Assert.IsTrue(viewModel.IsRunning);
        Assert.IsTrue(viewModel.TripLatched);
    }

    [TestMethod]
    public async Task RelayResetClearsTripButPreservesInjectionProfileAndRunState()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.InjectAgFault();
        for (var index = 0; index < 5; index++)
            viewModel.Tick();

        Assert.IsTrue(viewModel.TripLatched);
        var profile = viewModel.ProfileNameText;
        var fingerprint = viewModel.InjectionFingerprintText;

        viewModel.ResetRelay();

        Assert.IsFalse(viewModel.TripLatched);
        Assert.IsTrue(viewModel.IsRunning);
        Assert.AreEqual(profile, viewModel.ProfileNameText);
        Assert.AreEqual(fingerprint, viewModel.InjectionFingerprintText);
        StringAssert.Contains(viewModel.StatusText, "remains RUNNING");
    }

    [TestMethod]
    public async Task ApplyingRelaySettingsPreservesTheSeparatelyConfiguredSource()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.SelectedPreset = "Three-phase fault";
        viewModel.ToggleRun();
        viewModel.Tick();
        var injectionFingerprint = viewModel.InjectionFingerprintText;

        viewModel.SettingsEditor.GroupName = "GROUP TEST";
        viewModel.SettingsEditor.RevisionText = "7";
        viewModel.SettingsEditor.Phase50.PickupText = "6.5";
        viewModel.SettingsEditor.Earth50.DelayMsText = "125";
        viewModel.ApplySettings();

        Assert.AreEqual("GROUP TEST · REV 7", viewModel.SettingsGroupText);
        Assert.IsTrue(viewModel.IsRunning);
        Assert.AreEqual("Three-phase fault", viewModel.ProfileNameText);
        Assert.AreEqual(injectionFingerprint, viewModel.InjectionFingerprintText);
        Assert.IsFalse(viewModel.TripLatched);
        StringAssert.Contains(viewModel.SettingsEditorStatus, "APPLIED");
    }

    [TestMethod]
    public async Task FullLaboratoryResetReturnsToStoppedNormalProfileButRetainsRelaySettings()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.SettingsEditor.GroupName = "GROUP B";
        viewModel.SettingsEditor.RevisionText = "2";
        viewModel.ApplySettings();
        viewModel.InjectAgFault();
        viewModel.ToggleSmvDegradation();

        viewModel.ResetLaboratory();

        Assert.IsFalse(viewModel.IsRunning);
        Assert.IsFalse(viewModel.FaultActive);
        Assert.IsFalse(viewModel.SmvDegraded);
        Assert.IsFalse(viewModel.TripLatched);
        Assert.AreEqual("START INJECTION", viewModel.RunButtonText);
        Assert.AreEqual("Normal balanced", viewModel.ProfileNameText);
        Assert.AreEqual("GROUP B · REV 2", viewModel.SettingsGroupText);
    }
}
