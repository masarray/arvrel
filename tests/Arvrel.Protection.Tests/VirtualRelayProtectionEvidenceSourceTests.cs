using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualRelayProtectionEvidenceSourceTests
{
    [TestMethod]
    public void P13TripLog_ExposesPhaseFaultQuantityTimingAndContext()
    {
        var evidence = Read("src", "Arvrel.App", "MainWindow.RelayProtectionEvidence.cs");

        foreach (var token in new[]
                 {
                     "TripSummary",
                     "TripFault",
                     "TripSystem",
                     "PHASE",
                     "PICKUP",
                     "P→T",
                     "QuantitySymbol",
                     "TripQuantity",
                     "SettingGroup",
                     "SettingRevision",
                     "SettingsFingerprint",
                     "SourceSummary",
                     "TrustCode",
                     "TripPermitted"
                 })
        {
            StringAssert.Contains(evidence, token);
        }

        StringAssert.Contains(evidence, "ProtectionSnapshot.LatchedOperation");
        StringAssert.Contains(evidence, "RelayFaultPhaseLabel(latched.PhaseA, latched.PhaseB, latched.PhaseC, latched.Earth)");
        StringAssert.Contains(evidence, "IABC  NOT LATCHED");
        StringAssert.Contains(evidence, "I OP / PHASE ARE ENGINE-LATCHED");
        StringAssert.Contains(evidence, "EXACT TRIP SAMPLE");
    }

    [TestMethod]
    public void P13EventLog_IdentifiesEventClassAndLinksTripEvidence()
    {
        var evidence = Read("src", "Arvrel.App", "MainWindow.RelayProtectionEvidence.cs");

        StringAssert.Contains(evidence, "EVENT {item.Sequence:000} · {item.Code}");
        StringAssert.Contains(evidence, "PROTECTION");
        StringAssert.Contains(evidence, "PROTECTION BLOCK");
        StringAssert.Contains(evidence, "CONFIGURATION");
        StringAssert.Contains(evidence, "SOURCE / PROCESS BUS");
        StringAssert.Contains(evidence, "BINARY I/O");
        StringAssert.Contains(evidence, "OPERATOR ACTION");
        StringAssert.Contains(evidence, "FindRelayTripEvidence(item.Timestamp)");
        StringAssert.Contains(evidence, "▲ OLDER · ▼ NEWER");
    }

    [TestMethod]
    public void P13EvidenceHistory_PersistsAcrossProtectionResetWithoutNewAuthority()
    {
        var evidence = Read("src", "Arvrel.App", "MainWindow.RelayProtectionEvidence.cs");
        var p6 = Read("src", "Arvrel.App", "MainWindow.P6VirtualRelay.cs");

        StringAssert.Contains(p6, "InitializeRelayProtectionEvidence();");
        StringAssert.Contains(p6, "HandleRelayEvidenceHardwareKey(e.Key)");
        StringAssert.Contains(p6, "RouteRelayEvidenceHostPage();");
        StringAssert.Contains(p6, "NotifyRelayEvidenceReset();");
        StringAssert.Contains(evidence, "Protection RESET clears the latch, not the stored fault/event history.");
        StringAssert.Contains(evidence, "_relayTripEvidenceLog");

        Assert.IsFalse(evidence.Contains("new ProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(evidence.Contains("UpdateSettings", StringComparison.Ordinal));
        Assert.IsFalse(evidence.Contains("TripRequested =", StringComparison.Ordinal));
        Assert.IsFalse(evidence.Contains("TripLatched =", StringComparison.Ordinal));
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
