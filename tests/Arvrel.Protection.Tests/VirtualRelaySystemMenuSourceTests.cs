using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualRelaySystemMenuSourceTests
{
    [TestMethod]
    public void P11RelayMenu_UsesPractitionerFacingSystemVocabulary()
    {
        var extension = Read("src", "Arvrel.App", "MainWindow.RelayHmi.SystemMenu.cs");
        var hmi = Read("src", "Arvrel.App", "MainWindow.RelayHmi.cs");
        var hardware = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml.cs");

        foreach (var label in new[]
                 {
                     "MEASUREMENTS",
                     "PROTECTION",
                     "EVENT LOG / RECORDS",
                     "SETTINGS & CONFIG",
                     "SYSTEM / MONITOR I/O",
                     "MONITOR I/O / PROCESS BUS",
                     "DEVICE / SYSTEM INFO"
                 })
        {
            StringAssert.Contains(extension, label);
        }

        // Navigation remains one P1 state machine rather than a parallel menu.
        StringAssert.Contains(extension, "RelayRootMenu[0]");
        StringAssert.Contains(extension, "RelayDiagnosticsMenu[0]");
        Assert.IsFalse(extension.Contains("ProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(extension.Contains("UpdateSettings", StringComparison.Ordinal));

        // Every physical key on the faceplate remains actionable through P1.
        foreach (var key in new[]
                 {
                     "F1", "F2", "F3", "F4", "F5", "Home", "Menu", "Up", "Down",
                     "Left", "Right", "Ok", "Back", "Favorite"
                 })
        {
            StringAssert.Contains(hardware, key);
        }

        StringAssert.Contains(hmi, "HandleRelayHardwareKey(VirtualRelayHardwareKey key)");
        StringAssert.Contains(hmi, "SetSoftKeys");
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
