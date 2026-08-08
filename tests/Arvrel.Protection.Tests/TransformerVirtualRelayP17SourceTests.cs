using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerVirtualRelayP17SourceTests
{
    [TestMethod]
    public void Faceplate_ReusesArvrelRelayLanguageForTransformerFunctions()
    {
        var code = Read("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayControl.cs");

        StringAssert.Contains(code, "ARVREL 87T");
        StringAssert.Contains(code, "TRANSFORMER DIFFERENTIAL RELAY");
        StringAssert.Contains(code, "TR-87T");
        StringAssert.Contains(code, "87T-HS");
        StringAssert.Contains(code, "REF HV");
        StringAssert.Contains(code, "REF LV");
        StringAssert.Contains(code, "HARMONICS H2 / H5");
        StringAssert.Contains(code, "SV PAIR / TRUST");
        StringAssert.Contains(code, "VIRTUAL TRIP OUTPUT ONLY · NO GOOSE · NO BREAKER OUTPUT");
    }

    [TestMethod]
    public void Faceplate_HasRealOperatorKeysAndAnnunciation()
    {
        var code = Read("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayControl.cs");

        foreach (var label in new[] { "HEALTHY", "PICKUP", "TRIP", "87T", "87T-HS", "REF HV", "REF LV", "BLOCK" })
            StringAssert.Contains(code, label);
        foreach (var key in new[] { "F1", "F2", "F3", "F4", "F5", "Up", "Down", "Enter", "Next", "Cancel" })
            StringAssert.Contains(code, key);

        StringAssert.Contains(code, "SoftKey(\"F1\", \"MEAS\"");
        StringAssert.Contains(code, "SoftKey(\"F2\", \"EVENT\"");
        StringAssert.Contains(code, "SoftKey(\"F3\", \"RECORD\"");
        StringAssert.Contains(code, "SoftKey(\"F4\", \"ENG\"");
        StringAssert.Contains(code, "SoftKey(\"F5\", \"RESET\"");
    }

    [TestMethod]
    public void Faceplate_ConsumesAuthoritativeRuntimeSnapshotWithoutProtectionReimplementation()
    {
        var faceplate = Read("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayControl.cs");
        var bridge = Read("src", "Arvrel.App", "TransformerIedWindow.P17.cs");
        var main = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(faceplate, "UpdateSnapshot(TransformerProtectionRuntimeSnapshot? snapshot)");
        StringAssert.Contains(faceplate, "snapshot.Protection");
        StringAssert.Contains(faceplate, "protection?.Differential");
        StringAssert.Contains(bridge, "FaceplateSnapshotChanged");
        StringAssert.Contains(bridge, "_lastSnapshot");
        StringAssert.Contains(bridge, "_runtime.Reset()");
        StringAssert.Contains(main, "window.FaceplateSnapshotChanged += TransformerWorkspace_FaceplateSnapshotChanged;");
        StringAssert.Contains(main, "_transformerFaceplate?.UpdateSnapshot(e.Snapshot);");

        Assert.IsFalse(faceplate.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(bridge.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(faceplate.Contains("StandardSlopeThresholdPu", StringComparison.Ordinal));
        Assert.IsFalse(faceplate.Contains("MinimumBiasIncreasePu", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Faceplate_SeparatesOperatorAndEngineeringWorkspaces()
    {
        var main = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(main, "Operator front panel");
        StringAssert.Contains(main, "Open engineering workspace");
        StringAssert.Contains(main, "window.Show();");
        StringAssert.Contains(main, "if (_transformerWorkspaceWindow is { IsVisible: true } existing)");
        StringAssert.Contains(main, "existing.Activate();");
        StringAssert.Contains(main, "_transformerWorkspaceWindow.Close();");
        StringAssert.Contains(main, "TransformerPublicSelfTest.RunAll()");
        Assert.IsFalse(main.Contains("window.ShowDialog();", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RecordsPage_UsesExistingDeterministicPublicSelfTest()
    {
        var faceplate = Read("src", "Arvrel.App", "Controls", "Transformer", "TransformerVirtualRelayControl.cs");
        var main = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(faceplate, "RECORDS / SELF-TEST");
        StringAssert.Contains(faceplate, "RUN 10-SCENARIO SELF-TEST");
        StringAssert.Contains(faceplate, "transformer-public-beta-v1");
        StringAssert.Contains(main, "TransformerPublicSelfTest.RunAll()");
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
