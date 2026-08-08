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
        StringAssert.Contains(code, "OpenTransformerIedWorkspace();");
        StringAssert.Contains(code, "new TransformerIedWindow(_processBus)");
        StringAssert.Contains(code, "window.InitializeP14PractitionerUi();");
        StringAssert.Contains(code, "window.InitializeP15PublicTestUi();");
        StringAssert.Contains(code, "TransformerVirtualRelayControl");
        Assert.IsFalse(code.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TransformerSelection_ShowsOperatorRelayBeforeEngineeringWorkspace()
    {
        var code = Read("src", "Arvrel.App", "MainWindow.TransformerIed.cs");

        StringAssert.Contains(code, "ShowTransformerLanding();");
        StringAssert.Contains(code, "Virtual relay front panel is active");
        StringAssert.Contains(code, "No physical trip · no GOOSE · no breaker output");
        StringAssert.Contains(code, "10 deterministic core scenarios");
        StringAssert.Contains(code, "Applying the live/replay runtime still requires two");
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
