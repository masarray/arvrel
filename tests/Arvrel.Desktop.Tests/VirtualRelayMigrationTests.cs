using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class VirtualRelayMigrationTests
{
    [TestMethod]
    public void Faceplate_IsNativeAvaloniaAndUsesOneScaledHardwareCanvas()
    {
        var source = Read("src", "Arvrel.Desktop", "Controls", "VirtualRelayControl.axaml");
        _ = XDocument.Parse(source, LoadOptions.PreserveWhitespace);

        StringAssert.Contains(source, "<Viewbox Stretch=\"Uniform\"");
        StringAssert.Contains(source, "<Grid Width=\"560\" Height=\"700\"");
        StringAssert.Contains(source, "ARVREL");
        StringAssert.Contains(source, "ARV-650");
        StringAssert.Contains(source, "PROCESS BUS PROTECTION RELAY");
        StringAssert.Contains(source, "VIRTUAL TRIP OUTPUT ONLY");
        Assert.AreEqual(8, Count(source, "<controls:RelayLampControl"));

        Assert.IsFalse(source.Contains("<Image", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains(".png", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(source.Contains("DrawingBrush", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Geometry.Parse", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FaceplateAndLcd_BindToExistingPortableStateAndCommands()
    {
        var faceplate = Read("src", "Arvrel.Desktop", "Controls", "VirtualRelayControl.axaml");
        var lcd = Read("src", "Arvrel.Desktop", "Controls", "RelayLcdControl.axaml");
        var combined = faceplate + lcd;

        foreach (var binding in new[]
                 {
                     "{Binding AllowsTrip}",
                     "{Binding PickupActive}",
                     "{Binding TripLatched}",
                     "{Binding TripStateText}",
                     "{Binding PhaseAText}",
                     "{Binding PhaseBText}",
                     "{Binding PhaseCText}",
                     "{Binding ResidualText}",
                     "{Binding TrustStateText}",
                     "{Binding SettingsGroupText}",
                     "{Binding ResetRelayCommand}"
                 })
        {
            StringAssert.Contains(combined, binding);
        }
    }

    [TestMethod]
    public void Faceplate_PhaseAndEarthLampsUsePortableAnnunciationLatch()
    {
        var xaml = Read("src", "Arvrel.Desktop", "Controls", "VirtualRelayControl.axaml");
        var code = Read("src", "Arvrel.Desktop", "Controls", "VirtualRelayControl.axaml.cs");

        StringAssert.Contains(xaml, "x:Name=\"Root\"");
        foreach (var name in new[] { "PhaseA", "PhaseB", "PhaseC", "Earth" })
        {
            StringAssert.Contains(xaml, $"IsActive=\"{{Binding {name}Active, ElementName=Root}}\"");
            StringAssert.Contains(xaml, $"Tone=\"{{Binding {name}Tone, ElementName=Root}}\"");
            StringAssert.Contains(code, $"StyledProperty<bool> {name}ActiveProperty");
            StringAssert.Contains(code, $"StyledProperty<RelayLampTone> {name}ToneProperty");
        }

        StringAssert.Contains(code, "private readonly RelayAnnunciationLatch _annunciationLatch = new()");
        StringAssert.Contains(code, "_annunciationLatch.Observe(tick.Protection)");
        StringAssert.Contains(code, "state == RelayLampState.Trip ? RelayLampTone.Red : RelayLampTone.Amber");
        StringAssert.Contains(code, "_annunciationLatch.Reset()");
        Assert.IsFalse(code.Contains("ProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("VirtualInjectionPresets", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RelayLcd_HasNativeMeasurementEventRecordSetupAndDiagnosticPages()
    {
        var xaml = Read("src", "Arvrel.Desktop", "Controls", "RelayLcdControl.axaml");
        var code = Read("src", "Arvrel.Desktop", "Controls", "RelayLcdControl.axaml.cs");
        _ = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);

        foreach (var page in new[]
                 {
                     "MeasurePage", "EventsPage", "RecordPage", "SetupPage", "DiagnosticsPage"
                 })
        {
            StringAssert.Contains(xaml, $"x:Name=\"{page}\"");
        }

        foreach (var heading in new[]
                 {
                     "MEASUREMENTS · SECONDARY",
                     "EVENT LOG · OPERATOR TRACE",
                     "TRIP RECORD · LAST OPERATION",
                     "ACTIVE CONFIGURATION",
                     "PROCESS-BUS DIAGNOSTICS"
                 })
        {
            StringAssert.Contains(xaml, heading);
        }

        StringAssert.Contains(code, "public enum RelayLcdPage");
        StringAssert.Contains(code, "public void ShowPage(RelayLcdPage page)");
        StringAssert.Contains(code, "public void NextPage()");
        StringAssert.Contains(code, "public void PreviousPage()");
        StringAssert.Contains(code, "RelayLcdPage.Diagnostics");
        Assert.IsFalse(code.Contains("System.Windows", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("ProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FaceplateHardware_ControlsAllLcdPagesAndNavigation()
    {
        var xaml = Read("src", "Arvrel.Desktop", "Controls", "VirtualRelayControl.axaml");
        var code = Read("src", "Arvrel.Desktop", "Controls", "VirtualRelayControl.axaml.cs");

        StringAssert.Contains(xaml, "<controls:RelayLcdControl x:Name=\"RelayLcd\"");
        foreach (var handler in new[]
                 {
                     "F1Button_Click", "F2Button_Click", "F3Button_Click", "F4Button_Click", "F5Button_Click",
                     "HomeButton_Click", "MenuButton_Click", "BackButton_Click", "StarButton_Click",
                     "PreviousPageButton_Click", "NextPageButton_Click", "OkButton_Click"
                 })
        {
            StringAssert.Contains(xaml, $"Click=\"{handler}\"");
            StringAssert.Contains(code, $"private void {handler}");
        }

        StringAssert.Contains(code, "_relayLcd?.ShowPage(RelayLcdPage.Measure)");
        StringAssert.Contains(code, "_relayLcd?.ShowPage(RelayLcdPage.Events)");
        StringAssert.Contains(code, "_relayLcd?.ShowPage(RelayLcdPage.Records)");
        StringAssert.Contains(code, "_relayLcd?.ShowPage(RelayLcdPage.Setup)");
        StringAssert.Contains(code, "_relayLcd?.ShowPage(RelayLcdPage.Diagnostics)");
        StringAssert.Contains(code, "_relayLcd?.PreviousPage()");
        StringAssert.Contains(code, "_relayLcd?.NextPage()");
    }

    [TestMethod]
    public void Lamp_UsesOneReusablePhysicalAssemblyAndNoPlatformSpecificApi()
    {
        var xaml = Read("src", "Arvrel.Desktop", "Controls", "RelayLampControl.axaml");
        var code = Read("src", "Arvrel.Desktop", "Controls", "RelayLampControl.axaml.cs");
        _ = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);

        StringAssert.Contains(xaml, "x:Name=\"Halo\"");
        StringAssert.Contains(xaml, "x:Name=\"Lens\"");
        StringAssert.Contains(xaml, "x:Name=\"Highlight\"");
        StringAssert.Contains(code, "StyledProperty<bool> IsActiveProperty");
        StringAssert.Contains(code, "StyledProperty<RelayLampTone> ToneProperty");
        StringAssert.Contains(code, "RelayLampTone.Amber");
        StringAssert.Contains(code, "RelayLampTone.Red");
        StringAssert.Contains(code, "RelayLampTone.Blue");
        StringAssert.Contains(code, "Color.Parse(\"#2EEA63\")");

        Assert.IsFalse(code.Contains("System.Windows", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("Dispatcher", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("ProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainWindow_MountsFaceplateAsFirstRelayWorkspaceTab()
    {
        var source = Read("src", "Arvrel.Desktop", "MainWindow.axaml.cs");

        StringAssert.Contains(source, "InstallVirtualRelayFaceplate()");
        StringAssert.Contains(source, "GetVisualDescendants()");
        StringAssert.Contains(source, "string.Equals(tab.Header?.ToString(), \"RELAY\"");
        StringAssert.Contains(source, "relayTabs.Items.Insert(0");
        StringAssert.Contains(source, "Header = \"FACEPLATE\"");
        StringAssert.Contains(source, "relayTabs.SelectedIndex = 0");
        StringAssert.Contains(source, "DataContext = DataContext");
    }

    private static int Count(string source, string token)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }

        return count;
    }

    private static string Read(params string[] segments)
        => File.ReadAllText(Locate(segments));

    private static string Locate(params string[] segments)
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
