using Arvrel.Protection.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class AlgorithmPolicyValidatorTests
{
    [TestMethod]
    public void SafeTemplate_Passes()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i >= setting("I>>")
              dropout = i < setting("I>>") * 0.95
              trip = pickup && smv.allowsTrip
            }
            """;
        var result = AlgorithmPolicyValidator.Validate(source);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    [DataRow("50P-1")]
    [DataRow("51P")]
    [DataRow("50N")]
    [DataRow("51N")]
    [DataRow("67P")]
    [DataRow("67N")]
    [DataRow("27")]
    [DataRow("59")]
    [DataRow("59N")]
    public void GeneratedStandardSourcePassesShadowPolicy(string element)
    {
        var source = AlgorithmSourceCatalog.Build(element, new ProtectionSettings());
        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.AreEqual(64, result.ContentHash.Length);
    }

    [TestMethod]
    public void DirectionalSourceWithoutPolarizingSupervisionGetsWarning()
    {
        const string source = """
            element "67" {
              phasor current = I1
              directionSelected = direction(1, "Forward")
              dropout = !directionSelected
              trip = directionSelected && smv.allowsTrip
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Warnings.Any(item => item.Contains("polarizing", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void FileAccess_IsRejected()
    {
        const string source = "element \"50P\" { input i = IA.rms1c trip = File.ReadAllText(\"x\") && smv.allowsTrip }";
        var result = AlgorithmPolicyValidator.Validate(source);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("File.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MissingSmvTripGate_IsRejected()
    {
        const string source = "element \"50P\" { input i = IA.rms1c trip = i > 4 }";
        var result = AlgorithmPolicyValidator.Validate(source);
        Assert.IsFalse(result.IsValid);
    }
}
