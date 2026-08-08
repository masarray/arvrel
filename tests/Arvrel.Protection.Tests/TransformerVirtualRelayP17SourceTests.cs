using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerVirtualRelayP17SourceTests
{
    [TestMethod]
    public void TransformerFaceplate_InstantiatesExactSharedOcrHardware()
    {
        var main = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");
        var sharedXaml = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml");
        var sharedCode = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml.cs");

        StringAssert.Contains(main, "using Arvrel.App.Controls.VirtualRelay;");
        StringAssert.Contains(main, "private VirtualRelayControl? _transformerFaceplate;");
        StringAssert.Contains(main, "_transformerFaceplate = new VirtualRelayControl");
        StringAssert.Contains(main, "No Transformer-specific shell, card, operator rail, button geometry or LED");
        StringAssert.Contains(sharedCode, "It owns hardware geometry and visual");
        StringAssert.Contains(sharedXaml, "VrOuterShellBrush");
        StringAssert.Contains(sharedXaml, "VrFrontFaceBrush");
        StringAssert.Contains(sharedXaml, "VrHardwareKeyStyle");
        StringAssert.Contains(sharedXaml, "VrFunctionKeyStyle");
        StringAssert.Contains(sharedXaml, "RelayLampControl");

        Assert.IsFalse(File.Exists(FindPath("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayControl.cs")),
            "P17 must not keep an improvised Transformer-specific relay hardware clone.");
    }

    [TestMethod]
    public void TransformerPresenter_OnlyRemapsIdentityLabelsAndAuthoritativeState()
    {
        var presenter = Read("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayPresenter.cs");
        var sharedCode = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml.cs");

        StringAssert.Contains(sharedCode, "ApplyTextOverrides");
        StringAssert.Contains(presenter, "[\" 650\"] = \" 87T\"");
        StringAssert.Contains(presenter, "[\"PROCESS BUS PROTECTION RELAY\"] = \"TRANSFORMER DIFFERENTIAL RELAY\"");
        StringAssert.Contains(presenter, "[\"PHASE A\"] = \"87T\"");
        StringAssert.Contains(presenter, "[\"PHASE B\"] = \"87T-HS\"");
        StringAssert.Contains(presenter, "[\"PHASE C\"] = \"REF HV\"");
        StringAssert.Contains(presenter, "[\"EARTH\"] = \"REF LV\"");
        StringAssert.Contains(presenter, "[\"SMV BLOCK\"] = \"BLOCK\"");
        StringAssert.Contains(presenter, "TransformerProtectionRuntimeSnapshot? _snapshot");
        StringAssert.Contains(presenter, "var protection = snapshot?.Protection;");

        Assert.IsFalse(presenter.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(presenter.Contains("StandardSlopeThresholdPu", StringComparison.Ordinal));
        Assert.IsFalse(presenter.Contains("MinimumBiasIncreasePu", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TransformerPresenter_ReusesOcrHardwareKeysAndLamps()
    {
        var presenter = Read("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayPresenter.cs");
        var xaml = Read("src", "Arvrel.App", "Controls", "VirtualRelay", "VirtualRelayControl.xaml");

        foreach (var key in new[] { "F1", "F2", "F3", "F4", "F5", "Up", "Down", "Enter", "Next", "Cancel" })
            StringAssert.Contains(xaml, key);

        foreach (var lamp in new[] { "HealthyLamp", "PickupLamp", "TripLamp", "PhaseALamp", "PhaseBLamp", "PhaseCLamp", "EarthLamp", "BlockLamp" })
        {
            StringAssert.Contains(xaml, lamp);
            StringAssert.Contains(presenter, $"_relay.{lamp}.Lens");
        }

        StringAssert.Contains(presenter, "F1\": button.ToolTip = \"Transformer measurements\"");
        StringAssert.Contains(presenter, "F2\": button.ToolTip = \"Transformer events\"");
        StringAssert.Contains(presenter, "F3\": button.ToolTip = \"Transformer records and self-test\"");
        StringAssert.Contains(presenter, "F4\": button.ToolTip = \"Transformer engineering\"");
        StringAssert.Contains(presenter, "F5\": button.ToolTip = \"Reset transformer runtime\"");
    }

    [TestMethod]
    public void Faceplate_ConsumesExistingRuntimeBridgeAndKeepsEngineeringNonModal()
    {
        var bridge = Read("src", "Arvrel.App", "TransformerIedWindow.P17.cs");
        var main = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(bridge, "FaceplateSnapshotChanged");
        StringAssert.Contains(bridge, "_lastSnapshot");
        StringAssert.Contains(bridge, "_runtime.Reset()");
        StringAssert.Contains(main, "window.InitializeP17FaceplateBridge();");
        StringAssert.Contains(main, "window.FaceplateSnapshotChanged += TransformerWorkspace_FaceplateSnapshotChanged;");
        StringAssert.Contains(main, "_transformerFaceplatePresenter?.UpdateSnapshot(e.Snapshot);");
        StringAssert.Contains(main, "window.Show();");
        StringAssert.Contains(main, "if (_transformerWorkspaceWindow is { IsVisible: true } existing)");
        StringAssert.Contains(main, "TransformerPublicSelfTest.RunAll()");
        Assert.IsFalse(main.Contains("window.ShowDialog();", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RecordsPage_UsesExistingDeterministicPublicSelfTest()
    {
        var presenter = Read("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayPresenter.cs");
        var main = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(presenter, "RECORDS / SELF-TEST");
        StringAssert.Contains(presenter, "10 DETERMINISTIC CASES");
        StringAssert.Contains(main, "TransformerPublicSelfTest.RunAll()");
    }

    private static string Read(params string[] segments)
        => File.ReadAllText(FindPath(segments));

    private static string FindPath(params string[] segments)
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
                if (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate)!))
                    return candidate;
            }
        }

        return Path.Combine(new[] { Environment.CurrentDirectory }.Concat(segments).ToArray());
    }
}
