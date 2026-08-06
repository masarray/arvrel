using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class VirtualRelayFaceplateSourceTests
{
    [TestMethod]
    public void Faceplate_IsNativeAvaloniaAndContainsCompleteHardwareRegions()
    {
        var faceplate = Read("src", "Arvrel.Desktop", "Controls", "VirtualRelayFaceplate.axaml");
        var lamp = Read("src", "Arvrel.Desktop", "Controls", "RelayLamp.axaml");

        _ = XDocument.Parse(faceplate, LoadOptions.PreserveWhitespace);
        _ = XDocument.Parse(lamp, LoadOptions.PreserveWhitespace);

        StringAssert.Contains(faceplate, "<Viewbox Stretch=\"Uniform\"");
        StringAssert.Contains(faceplate, "ARVREL");
        StringAssert.Contains(faceplate, "PROCESS BUS PROTECTION RELAY");
        StringAssert.Contains(faceplate, "BAY 12 · FEEDER");
        StringAssert.Contains(faceplate, "HEALTHY");
        StringAssert.Contains(faceplate, "PICKUP");
        StringAssert.Contains(faceplate, "TRIP");
        StringAssert.Contains(faceplate, "SMV BLOCK");
        StringAssert.Contains(faceplate, "F1");
        StringAssert.Contains(faceplate, "F5");
        StringAssert.Contains(faceplate, "RESET");
        StringAssert.Contains(faceplate, "ResetRelayCommand");

        Assert.AreEqual(8, Count(faceplate, "<controls:RelayLamp "));
        Assert.IsFalse(faceplate.Contains(".png", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(faceplate.Contains("<Image", StringComparison.Ordinal));
        Assert.IsFalse(faceplate.Contains("System.Windows", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Lamp_UsesOneSharedBezelCavityLensAndOpticalHighlight()
    {
        var lamp = Read("src", "Arvrel.Desktop", "Controls", "RelayLamp.axaml");
        var behavior = Read("src", "Arvrel.Desktop", "Controls", "RelayLamp.axaml.cs");

        StringAssert.Contains(lamp, "x:Name=\"Halo\"");
        StringAssert.Contains(lamp, "x:Name=\"Lens\"");
        StringAssert.Contains(lamp, "Width=\"22\"");
        StringAssert.Contains(lamp, "Width=\"17\"");
        StringAssert.Contains(lamp, "Width=\"12.5\"");
        StringAssert.Contains(lamp, "RadialGradientBrush");

        StringAssert.Contains(behavior, "StyledProperty<bool> IsOnProperty");
        StringAssert.Contains(behavior, "StyledProperty<Color> ActiveColorProperty");
        StringAssert.Contains(behavior, "Halo.Opacity = 0.72");
        StringAssert.Contains(behavior, "Lens.Fill = new SolidColorBrush(ActiveColor)");
        Assert.IsFalse(behavior.Contains("System.Windows", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainWindow_MountsFaceplateAsFirstOperatorTab()
    {
        var source = Read("src", "Arvrel.Desktop", "MainWindow.axaml.cs");

        StringAssert.Contains(source, "InstallVirtualRelayFaceplate()");
        StringAssert.Contains(source, "new VirtualRelayFaceplate");
        StringAssert.Contains(source, "Header = \"FACEPLATE\"");
        StringAssert.Contains(source, "relayTabs.Items.Insert(0");
        StringAssert.Contains(source, "relayTabs.SelectedIndex = 0");
        StringAssert.Contains(source, "DataContextChanged");
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

        throw new FileNotFoundException($"Unable to locate {Path.Combine(segments)}.");
    }
}
