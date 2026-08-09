using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualRelayOperatorExperienceSourceTests
{
    [TestMethod]
    public void P12IoModel_SeparatesSimulatedInputsFromProtectionAuthoritativeOutputs()
    {
        var model = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayOperatorModel.cs");
        var experience = Read("src", "Arvrel.App", "MainWindow.RelayOperatorExperience.cs");

        StringAssert.Contains(model, "VirtualRelayIoAuthority.SimulatedHmiOnly");
        StringAssert.Contains(model, "VirtualRelayIoAuthority.ProtectionAuthoritative");
        StringAssert.Contains(model, "VIRTUAL TRIP");
        StringAssert.Contains(model, "snapshot.TripLatched");
        StringAssert.Contains(model, "SMV BLOCK");
        StringAssert.Contains(model, "!snapshot.SmvTrust.AllowsTrip");
        StringAssert.Contains(model, "Virtual laboratory contact; not wired into protection equations.");

        StringAssert.Contains(experience, "Selected BO is protection-authoritative and read-only from the relay HMI.");
        Assert.IsFalse(model.Contains("UpdateSettings", StringComparison.Ordinal));
        Assert.IsFalse(model.Contains("ProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P12AlarmWorkflow_KeepsAckSeparateFromProtectionReset()
    {
        var model = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayOperatorModel.cs");
        var experience = Read("src", "Arvrel.App", "MainWindow.RelayOperatorExperience.cs");
        var p6 = Read("src", "Arvrel.App", "MainWindow.P6VirtualRelay.cs");
        var resetAuthority = Read("src", "Arvrel.App", "MainWindow.RelayResetSeparation.cs");

        StringAssert.Contains(model, "public int AcknowledgeAll()");
        StringAssert.Contains(model, "public int ClearInactiveAcknowledged()");
        StringAssert.Contains(experience, "RESET and ACK are intentionally different operations");
        StringAssert.Contains(experience, "alarm history retained until ACK/CLEAR");
        StringAssert.Contains(experience, "RESET DOES NOT ACK");
        StringAssert.Contains(p6, "ExecuteRelayResetCommand();");
        Assert.IsFalse(p6.Contains("Reset_Click(sender, e);", StringComparison.Ordinal));
        StringAssert.Contains(resetAuthority, "NotifyRelayOperatorReset();");
    }

    [TestMethod]
    public void P12SettingGroups_ReuseAuthoritativeSettingsApplySequence()
    {
        var experience = Read("src", "Arvrel.App", "MainWindow.RelayOperatorExperience.cs");

        StringAssert.Contains(experience, "GROUP A");
        StringAssert.Contains(experience, "GROUP B");
        StringAssert.Contains(experience, "RequireRelayAccess(VirtualRelayAccessLevel.Engineer, \"switch protection setting group\")");
        StringAssert.Contains(experience, "_internalEngine.UpdateSettings(settings, keepTripLatch: false)");
        StringAssert.Contains(experience, "await RecreateProcessBusAsync().ConfigureAwait(true)");
        StringAssert.Contains(experience, "ResetTransitionMarkers()");
        Assert.IsFalse(experience.Contains("new ProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P12ProgrammableLeds_RemainPresentationOnlyAndDefaultToPhaseEarth()
    {
        var experience = Read("src", "Arvrel.App", "MainWindow.RelayOperatorExperience.cs");
        var annunciation = Read("src", "Arvrel.App", "MainWindow.RelayAnnunciation.cs");
        var labels = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.Operator.cs");

        foreach (var signal in new[] { "PhaseA", "PhaseB", "PhaseC", "Earth", "SmvBlock", "Healthy", "ExternalAlarm", "SettingGroupB" })
            StringAssert.Contains(experience, $"VirtualRelayLedSignal.{signal}");

        StringAssert.Contains(experience, "RequireRelayAccess(VirtualRelayAccessLevel.Engineer, \"change programmable LED assignment\")");
        StringAssert.Contains(annunciation, "RefreshRelayProgrammableAnnunciation(indication, snapshot)");
        StringAssert.Contains(annunciation, "P6AnnunciationLampState.Green => HealthyBrush");
        StringAssert.Contains(labels, "SetProgrammableLampLabels");
        Assert.IsFalse(labels.Contains("ProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P12AccessProfiles_AreExplicitlyTrainingProfilesNotSecurityAuthentication()
    {
        var experience = Read("src", "Arvrel.App", "MainWindow.RelayOperatorExperience.cs");
        var model = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayOperatorModel.cs");

        StringAssert.Contains(model, "View");
        StringAssert.Contains(model, "Operator");
        StringAssert.Contains(model, "Engineer");
        StringAssert.Contains(experience, "NO PASSWORD / SECURITY CLAIM");
        StringAssert.Contains(experience, "This is a training profile, not authentication/security.");
    }

    private static string Read(params string[] segments)
        => File.ReadAllText(Locate(segments));

    private static string Locate(string[] segments)
    {
        var starts = new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory)
        };

        foreach (var start in starts)
        {
            for (var current = start; current is not null; current = current.Parent)
            {
                var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException($"Unable to locate {Path.Combine(segments)} from the test workspace.");
    }
}
