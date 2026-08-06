using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualRelayP6SourceTests
{
    [TestMethod]
    public void P6Xaml_IsWellFormedAndUsesOneScaledHardwareCanvas()
    {
        var relay = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml");
        var lamp = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "RelayLampControl.xaml");
        var tokens = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayTokens.xaml");

        _ = XDocument.Parse(relay, LoadOptions.PreserveWhitespace);
        _ = XDocument.Parse(lamp, LoadOptions.PreserveWhitespace);
        _ = XDocument.Parse(tokens, LoadOptions.PreserveWhitespace);

        StringAssert.Contains(relay, "<Viewbox Stretch=\"Uniform\"");
        StringAssert.Contains(relay, "<Grid Width=\"520\" Height=\"680\"");
        StringAssert.Contains(relay, "ARVREL");
        StringAssert.Contains(relay, "PROCESS BUS PROTECTION RELAY");
        StringAssert.Contains(relay, "BAY 12 · FEEDER");

        Assert.IsFalse(relay.Contains("CharacterSpacing", StringComparison.Ordinal));
        Assert.IsFalse(relay.Contains("<Image", StringComparison.Ordinal));
        Assert.IsFalse(relay.Contains(".png", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(relay.Contains("DrawingBrush", StringComparison.Ordinal));
        Assert.IsFalse(relay.Contains("Geometry.Parse", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P6Faceplate_HasExpectedModularHardwareRegions()
    {
        var relay = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml");

        foreach (var label in new[]
                 {
                     "HEALTHY", "PICKUP", "TRIP", "PHASE A", "PHASE B",
                     "PHASE C", "EARTH", "SMV BLOCK", "F1", "F2", "F3",
                     "F4", "F5", "RESET", "VIRTUAL TRIP OUTPUT ONLY"
                 })
        {
            StringAssert.Contains(relay, label);
        }

        Assert.AreEqual(8, Count(relay, "<vr:RelayLampControl "));
        StringAssert.Contains(relay, "x:Name=\"LcdContentHost\"");
        StringAssert.Contains(relay, "x:Name=\"RelayFooterText\"");
        StringAssert.Contains(relay, "Style=\"{StaticResource VrFunctionKeyStyle}\"");
        StringAssert.Contains(relay, "Style=\"{StaticResource VrHardwareKeyStyle}\"");
        StringAssert.Contains(relay, "ToolTip=\"Up\"");
        StringAssert.Contains(relay, "ToolTip=\"Down\"");
        StringAssert.Contains(relay, "ToolTip=\"Enter\"");
        StringAssert.Contains(relay, "ToolTip=\"Next\"");
        StringAssert.Contains(relay, "ToolTip=\"Cancel\"");
    }

    [TestMethod]
    public void P6Lamp_SeparatesHaloBezelCavityLensAndHighlight()
    {
        var lamp = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "RelayLampControl.xaml");
        var behavior = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "RelayLampControl.xaml.cs");

        var haloIndex = lamp.IndexOf("x:Name=\"Halo\"", StringComparison.Ordinal);
        var bezelIndex = lamp.IndexOf("Width=\"19\"", haloIndex + 1, StringComparison.Ordinal);
        var cavityIndex = lamp.IndexOf("Width=\"14.5\"", bezelIndex + 1, StringComparison.Ordinal);
        var lensIndex = lamp.IndexOf("x:Name=\"Lens\"", cavityIndex + 1, StringComparison.Ordinal);
        var highlightIndex = lamp.IndexOf("x:Name=\"Highlight\"", lensIndex + 1, StringComparison.Ordinal);

        Assert.IsTrue(haloIndex >= 0);
        Assert.IsTrue(bezelIndex > haloIndex);
        Assert.IsTrue(cavityIndex > bezelIndex);
        Assert.IsTrue(lensIndex > cavityIndex);
        Assert.IsTrue(highlightIndex > lensIndex);

        StringAssert.Contains(lamp, "x:FieldModifier=\"public\"");
        StringAssert.Contains(lamp, "VrLampBezelBrush");
        StringAssert.Contains(lamp, "VrLampCavityBrush");
        StringAssert.Contains(lamp, "VrLampShadeBrush");
        StringAssert.Contains(behavior, "FillDescriptor?.AddValueChanged(Lens");
        StringAssert.Contains(behavior, "Halo.Effect = glow");
        StringAssert.Contains(behavior, "BlurRadius = 11.5");
        StringAssert.Contains(behavior, "ShadowDepth = 0");
    }

    [TestMethod]
    public void P6Buttons_HaveShallowPressAndNoBlueFocusNoise()
    {
        var tokens = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayTokens.xaml");

        StringAssert.Contains(tokens, "x:Key=\"VrHardwareKeyStyle\"");
        StringAssert.Contains(tokens, "TranslateTransform Y=\"0.6\"");
        StringAssert.Contains(tokens, "Property=\"Margin\" Value=\"1,0.6,1,3.4\"");
        StringAssert.Contains(tokens, "FocusVisualStyle\" Value=\"{x:Null}\"");
        Assert.IsFalse(tokens.Contains("IsKeyboardFocused", StringComparison.Ordinal));
        Assert.IsFalse(tokens.Contains("#77B7DE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(tokens.Contains("TranslateTransform Y=\"3\"", StringComparison.Ordinal));
        Assert.IsFalse(tokens.Contains("TranslateTransform Y=\"5\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P6Build_RetiresAllLegacyHardwareMutationOwners()
    {
        var project = Read("src", "Arvrel.App", "Arvrel.App.csproj");

        foreach (var retired in new[]
                 {
                     "MainWindow.RelayHardwarePresentation.cs",
                     "MainWindow.RelayFullFaceGloss.cs",
                     "MainWindow.RelayLedPresentation.cs",
                     "MainWindow.RelayPremiumButtonTuning.cs",
                     "Controls\\RelayIndicatorLampBehavior.cs",
                     "Controls\\RelayTactileButtonBehavior.cs"
                 })
        {
            StringAssert.Contains(project, $"<Compile Remove=\"{retired}\" />");
        }
    }

    [TestMethod]
    public void P6RuntimeMount_UsesExplicitApplicationAndWindowLifecycle()
    {
        var app = Read("src", "Arvrel.App", "App.xaml.cs");
        var adapter = Read("src", "Arvrel.App", "MainWindow.P6VirtualRelay.cs");

        StringAssert.Contains(app, "protected override void OnActivated(EventArgs e)");
        StringAssert.Contains(app, "window.InitializeP6VirtualRelay()");
        StringAssert.Contains(adapter, "if (!IsLoaded)");
        StringAssert.Contains(adapter, "Loaded += P6Window_Loaded");
        StringAssert.Contains(adapter, "InstallP6VirtualRelay()");
        StringAssert.Contains(adapter, "Native virtual relay faceplate mounted");
        StringAssert.Contains(adapter, "· P6");

        Assert.IsFalse(adapter.Contains("ModuleInitializer", StringComparison.Ordinal));
        Assert.IsFalse(adapter.Contains("RegisterClassHandler", StringComparison.Ordinal));
        Assert.IsFalse(adapter.Contains("Dispatcher.BeginInvoke", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P6Adapter_RebindsExistingStateAuthoritiesWithoutProtectionChanges()
    {
        var adapter = Read("src", "Arvrel.App", "MainWindow.P6VirtualRelay.cs");
        var annunciation = Read("src", "Arvrel.App", "MainWindow.RelayAnnunciation.cs");

        StringAssert.Contains(adapter, "relayHost.Child = relay");
        StringAssert.Contains(adapter, "LcdHeaderText = relay.LcdHeaderText");
        StringAssert.Contains(adapter, "HealthyLed = relay.HealthyLamp.Lens");
        StringAssert.Contains(adapter, "BlockLed = relay.BlockLamp.Lens");
        StringAssert.Contains(adapter, "InitializeRelayFaceplate()");
        StringAssert.Contains(adapter, "InitializeRelayMeasurementHome()");
        StringAssert.Contains(adapter, "Reset_Click(sender, e)");

        Assert.IsFalse(adapter.Contains("ProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(adapter.Contains("UpdateSettings", StringComparison.Ordinal));
        Assert.IsFalse(adapter.Contains("FaultActive", StringComparison.Ordinal));

        StringAssert.Contains(annunciation, "state-only authority");
        Assert.IsFalse(annunciation.Contains("TripLed.Width", StringComparison.Ordinal));
        Assert.IsFalse(annunciation.Contains("TripLed.Height", StringComparison.Ordinal));
        Assert.IsFalse(annunciation.Contains("RelayIndicatorLampBehavior", StringComparison.Ordinal));
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
