using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayHardwarePresentationSourceTests
{
    [TestMethod]
    public void ThreeDimensionalPresentation_ContainsRequiredPhysicalDepthLayers()
    {
        var source = File.ReadAllText(SourcePath());

        foreach (var token in new[]
                 {
                     "RelayMountBackground",
                     "RelayBodyBackground",
                     "RelayBodyInnerBevel",
                     "RelayBodyTopSheen",
                     "RelayBodyBottomLip",
                     "RecessTopShade",
                     "RecessLeftShade",
                     "RecessBottomHighlight",
                     "ApplyRecessedModule",
                     "TranslateTransform Y=\"3\"",
                     "x:Name=\"Base\"",
                     "x:Name=\"Face\"",
                     "x:Name=\"Gloss\"",
                     "x:Name=\"LowerLip\"",
                     "Property=\"IsPressed\""
                 })
        {
            StringAssert.Contains(source, token);
        }

        Assert.IsFalse(source.Contains("button.Effect = RelayKeyShadow", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("button.Effect = RelayResetShadow", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EmbeddedButtonTemplates_AreWellFormedXml()
    {
        var source = File.ReadAllText(SourcePath());
        var keyTemplate = ExtractRawString(source, "RelayKeyTemplateXaml");
        var resetTemplate = ExtractRawString(source, "RelayResetTemplateXaml");

        var keyDocument = XDocument.Parse(keyTemplate, LoadOptions.PreserveWhitespace);
        var resetDocument = XDocument.Parse(resetTemplate, LoadOptions.PreserveWhitespace);

        Assert.AreEqual("ControlTemplate", keyDocument.Root?.Name.LocalName);
        Assert.AreEqual("ControlTemplate", resetDocument.Root?.Name.LocalName);
        Assert.AreEqual(2, keyDocument.Descendants().Count(element => element.Name.LocalName == "Trigger" &&
            string.Equals(element.Attribute("Property")?.Value, "IsPressed", StringComparison.Ordinal) ||
            string.Equals(element.Attribute("Property")?.Value, "IsMouseOver", StringComparison.Ordinal)));
    }

    private static string ExtractRawString(string source, string fieldName)
    {
        var marker = $"private const string {fieldName} = \"\"\"";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Unable to find raw string field {fieldName}.");
        start += marker.Length;

        var end = source.IndexOf("\n\"\"\";", start, StringComparison.Ordinal);
        Assert.IsTrue(end >= start, $"Unable to find raw string terminator for {fieldName}.");
        return source[start..end].Trim();
    }

    private static string SourcePath()
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
                var candidate = Path.Combine(
                    current.FullName,
                    "src",
                    "Arvrel.App",
                    "MainWindow.RelayHardwarePresentation.cs");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException(
            "Unable to locate src/Arvrel.App/MainWindow.RelayHardwarePresentation.cs from the test workspace.");
    }
}
