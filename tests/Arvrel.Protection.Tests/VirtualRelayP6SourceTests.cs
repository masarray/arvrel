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
        StringAssert.Contains(relay, "<Grid Width=\"560\" Height=\"710\" Margin=\"8\"");
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
        StringAssert.Contains(relay, "Style=\"{StaticResource VrResetKeyStyle}\"");
        StringAssert.Contains(relay, "Style=\"{StaticResource VrHardwareKeyStyle}\"");
        StringAssert.Contains(relay, "ToolTip=\"Up\"");
        StringAssert.Contains(relay, "ToolTip=\"Down\"");
        StringAssert.Contains(relay, "ToolTip=\"Enter\"");
        StringAssert.Contains(relay, "ToolTip=\"Next\"");
        StringAssert.Contains(relay, "ToolTip=\"Cancel\"");
    }

    [TestMethod]
    public void P6P0Layout_ProvidesRealSpaceForFunctionRailAndNavigationDeck()
    {
        var relay = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml");
        var tokens = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayTokens.xaml");
        var adapter = Read("src", "Arvrel.App", "MainWindow.P6VirtualRelay.cs");

        foreach (var column in new[]
                 {
                     "<ColumnDefinition Width=\"112\" />",
                     "<ColumnDefinition Width=\"12\" />",
                     "<ColumnDefinition Width=\"250\" />",
                     "<ColumnDefinition Width=\"95\" />"
                 })
        {
            StringAssert.Contains(relay, column);
        }

        StringAssert.Contains(relay, "<RowDefinition Height=\"314\" />");
        StringAssert.Contains(relay, "<RowDefinition Height=\"174\" />");
        StringAssert.Contains(relay, "<StackPanel Margin=\"12,10\" HorizontalAlignment=\"Center\"");
        StringAssert.Contains(relay, "Height=\"148\"");
        Assert.AreEqual(3, Count(relay, "<RowDefinition Height=\"48\" />"));
        Assert.AreEqual(5, Count(relay, "Margin=\"2\" Tag=\"P6_NATIVE\""));

        StringAssert.Contains(tokens, "<Setter Property=\"Width\" Value=\"64\" />");
        StringAssert.Contains(tokens, "x:Key=\"VrResetKeyStyle\"");

        Assert.IsFalse(relay.Contains("Margin=\"19\"", StringComparison.Ordinal));
        StringAssert.Contains(adapter, "CalmP6WorkspaceChrome(relayHost)");
        StringAssert.Contains(adapter, "workspaceFrame.BorderThickness = new Thickness(0)");
        StringAssert.Contains(adapter, "workspaceFrame.Padding = new Thickness(0)");
        StringAssert.Contains(adapter, "workspaceFrame.Effect = null");
    }

    [TestMethod]
    public void P6P1Fascia_UsesBroadEdgeToEdgeGlossWithoutGeometryPatch()
    {
        var tokens = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayTokens.xaml");

        StringAssert.Contains(tokens, "P1: broad edge-to-edge fascia gloss");
        StringAssert.Contains(tokens, "x:Key=\"VrFrontFaceBrush\" StartPoint=\"0,0\" EndPoint=\"1,1\"");
        foreach (var offset in new[] { "0", "0.18", "0.42", "0.58", "0.78", "1" })
            StringAssert.Contains(tokens, $"Offset=\"{offset}\"");

        Assert.IsFalse(tokens.Contains("DrawingBrush", StringComparison.Ordinal));
        Assert.IsFalse(tokens.Contains("PathGeometry", StringComparison.Ordinal));
        Assert.IsFalse(tokens.Contains("Polygon", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P6Lamp_UsesOneRealisticOpticalStackForEveryActiveState()
    {
        var lamp = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "RelayLampControl.xaml");
        var behavior = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "RelayLampControl.xaml.cs");

        var haloIndex = lamp.IndexOf("x:Name=\"Halo\"", StringComparison.Ordinal);
        var bezelIndex = lamp.IndexOf("Width=\"21\"", haloIndex + 1, StringComparison.Ordinal);
        var cavityIndex = lamp.IndexOf("Width=\"16.6\"", bezelIndex + 1, StringComparison.Ordinal);
        var lensIndex = lamp.IndexOf("x:Name=\"Lens\"", cavityIndex + 1, StringComparison.Ordinal);
        var opticIndex = lamp.IndexOf("x:Name=\"LensOptic\"", lensIndex + 1, StringComparison.Ordinal);
        var rimIndex = lamp.IndexOf("x:Name=\"LensRim\"", opticIndex + 1, StringComparison.Ordinal);
        var lowerIndex = lamp.IndexOf("x:Name=\"LowerReflection\"", rimIndex + 1, StringComparison.Ordinal);
        var highlightIndex = lamp.IndexOf("x:Name=\"Highlight\"", lowerIndex + 1, StringComparison.Ordinal);

        Assert.IsTrue(haloIndex >= 0);
        Assert.IsTrue(bezelIndex > haloIndex);
        Assert.IsTrue(cavityIndex > bezelIndex);
        Assert.IsTrue(lensIndex > cavityIndex);
        Assert.IsTrue(opticIndex > lensIndex);
        Assert.IsTrue(rimIndex > opticIndex);
        Assert.IsTrue(lowerIndex > rimIndex);
        Assert.IsTrue(highlightIndex > lowerIndex);

        StringAssert.Contains(lamp, "x:FieldModifier=\"public\"");
        StringAssert.Contains(lamp, "Common metal bezel used by Healthy, Pickup, Trip and SMV Block");
        StringAssert.Contains(lamp, "VrLampBezelBrush");
        StringAssert.Contains(lamp, "VrLampCavityBrush");
        StringAssert.Contains(lamp, "VrLampShadeBrush");

        StringAssert.Contains(behavior, "FillDescriptor?.AddValueChanged(Lens");
        StringAssert.Contains(behavior, "LensOptic.Fill = CreateActiveLensBrush(emitted)");
        StringAssert.Contains(behavior, "BlurRadius = 8.5");
        StringAssert.Contains(behavior, "ShadowDepth = 0");
        StringAssert.Contains(behavior, "CreateActiveLensBrush(Color emitted)");
        StringAssert.Contains(behavior, "Mix(emitted, Colors.White, 0.78)");
        StringAssert.Contains(behavior, "Mix(emitted, Colors.Black, 0.46)");

        Assert.IsFalse(behavior.Contains("Lens.Width", StringComparison.Ordinal));
        Assert.IsFalse(behavior.Contains("Lens.Height", StringComparison.Ordinal));
        Assert.IsFalse(behavior.Contains("HealthyLamp", StringComparison.Ordinal));
        Assert.IsFalse(behavior.Contains("BlockLamp", StringComparison.Ordinal));
        Assert.IsFalse(behavior.Contains("TripLamp", StringComparison.Ordinal));
    }

    [TestMethod]
    public void P6Buttons_HaveSubtlePressAndNoBlueFocusNoise()
    {
        var tokens = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayTokens.xaml");

        StringAssert.Contains(tokens, "x:Key=\"VrHardwareKeyStyle\"");
        StringAssert.Contains(tokens, "TranslateTransform Y=\"0.25\"");
        StringAssert.Contains(tokens, "Property=\"Margin\" Value=\"1,0.25,1,3.75\"");
        StringAssert.Contains(tokens, "OverridesDefaultStyle\" Value=\"True\"");
        StringAssert.Contains(tokens, "Focusable\" Value=\"False\"");
        StringAssert.Contains(tokens, "IsTabStop\" Value=\"False\"");
        StringAssert.Contains(tokens, "FocusVisualStyle\" Value=\"{x:Null}\"");
        StringAssert.Contains(tokens, "BorderBrush\" Value=\"Transparent\"");

        Assert.IsFalse(tokens.Contains("IsKeyboardFocused", StringComparison.Ordinal));
        Assert.IsFalse(tokens.Contains("#77B7DE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(tokens.Contains("TranslateTransform Y=\"0.6\"", StringComparison.Ordinal));
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
        var resetAuthority = Read("src", "Arvrel.App", "MainWindow.RelayResetSeparation.cs");
        var annunciation = Read("src", "Arvrel.App", "MainWindow.RelayAnnunciation.cs");

        StringAssert.Contains(adapter, "relayHost.Child = relay");
        StringAssert.Contains(adapter, "LcdHeaderText = relay.LcdHeaderText");
        StringAssert.Contains(adapter, "HealthyLed = relay.HealthyLamp.Lens");
        StringAssert.Contains(adapter, "BlockLed = relay.BlockLamp.Lens");
        StringAssert.Contains(adapter, "InitializeRelayFaceplate()");
        StringAssert.Contains(adapter, "InitializeRelayMeasurementHome()");
        StringAssert.Contains(adapter, "ExecuteRelayResetCommand()");
        Assert.IsFalse(adapter.Contains("Reset_Click(sender, e)", StringComparison.Ordinal));
        StringAssert.Contains(resetAuthority, "ClosedLoopRelayResetTransaction.Execute");

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
