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
