using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayPremiumSurfaceSourceTests
{
    [TestMethod]
    public void FullFaceGloss_SpansEveryRelayBodyGridCell()
    {
        var source = File.ReadAllText(SourcePath("MainWindow.RelayFullFaceGloss.cs"));

        StringAssert.Contains(source, "Grid.SetRow(gloss, 0)");
        StringAssert.Contains(source, "Grid.SetColumn(gloss, 0)");
        StringAssert.Contains(source, "Grid.SetRowSpan(gloss, Math.Max(1, bodyGrid.RowDefinitions.Count))");
        StringAssert.Contains(source, "Grid.SetColumnSpan(gloss, Math.Max(1, bodyGrid.ColumnDefinitions.Count))");
        StringAssert.Contains(source, "#30FFFFFF");
        Assert.IsFalse(source.Contains("#66FFFFFF", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PremiumButtonTemplates_AreValidXamlAndUseShallowTravel()
    {
        var source = File.ReadAllText(SourcePath("MainWindow.RelayPremiumButtonTuning.cs"));
        var templates = ExtractRawTemplates(source).ToArray();

        Assert.AreEqual(2, templates.Length);
        foreach (var template in templates)
        {
            _ = XDocument.Parse(template);
            StringAssert.Contains(template, "Margin=\"1,3,1,0\"");
            StringAssert.Contains(template, "TranslateTransform Y=\"1\"");
            StringAssert.Contains(template, "ShadowDepth=\"1\"");
            Assert.IsFalse(template.Contains("TranslateTransform Y=\"3\"", StringComparison.Ordinal));
            Assert.IsFalse(template.Contains("Margin=\"1,5,1,0\"", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void PremiumButtonTuning_RunsAfterHardwarePresentation()
    {
        var source = File.ReadAllText(SourcePath("MainWindow.RelayPremiumButtonTuning.cs"));

        StringAssert.Contains(source, "if (!_relayHardwarePresentationInitialized)");
        StringAssert.Contains(source, "DispatcherPriority.SystemIdle");
        StringAssert.Contains(source, "MaximumRelayPremiumButtonAttempts");
        StringAssert.Contains(source, "button.Template = RelayPremiumKeyTemplate");
        StringAssert.Contains(source, "button.Template = RelayPremiumResetTemplate");
    }

    private static IEnumerable<string> ExtractRawTemplates(string source)
    {
        var matches = Regex.Matches(
            source,
            "private const string RelayPremium(?:Key|Reset)TemplateXaml = \\\"\\\"\\\"(?<xaml>.*?)\\\"\\\"\\\";",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        foreach (Match match in matches)
            yield return match.Groups["xaml"].Value.Trim();
    }

    private static string SourcePath(string fileName)
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
                var candidate = Path.Combine(current.FullName, "src", "Arvrel.App", fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException($"Unable to locate src/Arvrel.App/{fileName} from the test workspace.");
    }
}
