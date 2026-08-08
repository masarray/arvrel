using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class UnifiedIedSelectorSourceTests
{
    [TestMethod]
    public void UnifiedSelector_ExposesAllThreePublicIeds()
    {
        var code = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(code, "Protection Relay · OCR");
        StringAssert.Contains(code, "AVR · OLTC Controller");
        StringAssert.Contains(code, "Transformer Differential · 87T / REF");
        StringAssert.Contains(code, "UnifiedIedKind.TransformerDifferential");
        StringAssert.Contains(code, "public override string ToString() => DisplayName;");
        StringAssert.Contains(code, "DisplayMemberPath = nameof(UnifiedIedChoice.DisplayName)");
    }

    [TestMethod]
    public void UnifiedSelector_ReusesExistingOcrAvrAndTransformerAuthorities()
    {
        var code = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(code, "_iedTypeCombo.SelectionChanged -= IedTypeCombo_SelectionChanged;");
        StringAssert.Contains(code, "SelectIed(VirtualIedKind.AutomaticVoltageRegulator);");
        StringAssert.Contains(code, "SelectIed(VirtualIedKind.ProtectionRelay);");
        StringAssert.Contains(code, "new TransformerIedWindow(_processBus)");
        StringAssert.Contains(code, "window.InitializeP14PractitionerUi();");
        StringAssert.Contains(code, "window.InitializeP15PublicTestUi();");
        StringAssert.Contains(code, "window.InitializeP17FaceplateBridge();");
        StringAssert.Contains(code, "_transformerFaceplate = new VirtualRelayControl");
        Assert.IsFalse(code.Contains("TransformerVirtualRelayControl", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TransformerSelection_ShowsCompleteOcrOperatorWorkspaceBeforeEngineeringWorkspace()
    {
        var code = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");
        var workspace = Read("src", "Arvrel.App", "MainWindow.TransformerOcrWorkspace.cs");

        StringAssert.Contains(code, "ShowTransformerLanding();");
        StringAssert.Contains(code, "_protectionWorkspace.Visibility = Visibility.Visible;");
        StringAssert.Contains(code, "OperatingModeCombo.Visibility = Visibility.Visible;");
        StringAssert.Contains(code, "MountTransformerFaceplateIntoOcrWorkspace()");
        StringAssert.Contains(code, "OCR waveform / injection / phasor workspace is reused");
        StringAssert.Contains(code, "TransformerPublicSelfTest.RunAll()");
        StringAssert.Contains(code, "Applying the live/replay runtime");

        StringAssert.Contains(workspace, "_transformerSharedRelayHost.Child = _transformerFaceplate;");
        StringAssert.Contains(workspace, "_transformerSharedRelayHost.Child = _ocrWorkspaceRelay;");

        Assert.IsFalse(code.Contains("ShowTransformerLanding();\n        OpenTransformerIedWorkspace();", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("TransformerIedButton_Click", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("toolbar.Children.Insert", StringComparison.Ordinal));
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
