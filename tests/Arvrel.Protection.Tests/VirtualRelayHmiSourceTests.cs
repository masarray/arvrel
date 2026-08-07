using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualRelayHmiSourceTests
{
    [TestMethod]
    public void P1RelayHmi_UsesContextualHardwareAndHierarchicalMenus()
    {
        var hmi = Read("src", "Arvrel.App", "MainWindow.RelayHmi.cs");
        var p6 = Read("src", "Arvrel.App", "MainWindow.P6VirtualRelay.cs");
        var hardware = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml.cs");
        var project = Read("src", "Arvrel.App", "Arvrel.App.csproj");

        StringAssert.Contains(project, "<Compile Remove=\"MainWindow.RelayFaceplate.P2.cs\" />");
        StringAssert.Contains(hmi, "internal void HandleRelayHardwareKey(VirtualRelayHardwareKey key)");
        StringAssert.Contains(hmi, "RelayMenuLevel.Measurements");
        StringAssert.Contains(hmi, "RelayMenuLevel.Protection");
        StringAssert.Contains(hmi, "RelayMenuLevel.Records");
        StringAssert.Contains(hmi, "RelayMenuLevel.Diagnostics");
        StringAssert.Contains(hmi, "RelayLcdPage.CurrentMeasurements");
        StringAssert.Contains(hmi, "RelayLcdPage.VoltageMeasurements");
        StringAssert.Contains(hmi, "RelayLcdPage.SequenceMeasurements");
        StringAssert.Contains(hmi, "_relayFavoritePage");
        StringAssert.Contains(hmi, "SetLcdTrustState(\"TRIP LATCH\"");
        StringAssert.Contains(hmi, "SetLcdTrustState(\"SV BLOCK\"");

        StringAssert.Contains(hardware, "public event EventHandler<VirtualRelayHardwareKeyEventArgs>? HardwareKeyPressed");
        StringAssert.Contains(hardware, "softKeyStrip.Columns = 5");
        StringAssert.Contains(hardware, "public void SetSoftKeys(IReadOnlyList<VirtualRelaySoftKey> keys)");
        StringAssert.Contains(p6, "relay.HardwareKeyPressed += P6Relay_HardwareKeyPressed");
    }

    [TestMethod]
    public void P1RelayHmi_PreservesResetAsSeparateRelayAuthority()
    {
        var hardware = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml.cs");
        var p6 = Read("src", "Arvrel.App", "MainWindow.P6VirtualRelay.cs");

        StringAssert.Contains(hardware, "public event RoutedEventHandler? ResetRequested");
        StringAssert.Contains(hardware, "case \"RESET\": key = default; return false;");
        StringAssert.Contains(p6, "relay.ResetRequested += P6Relay_ResetRequested");
        StringAssert.Contains(p6, "=> Reset_Click(sender, e);");
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
