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

    [TestMethod]
    public void SmvGateInLineComment_CannotAuthorizeTrip()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i > 4
              dropout = i < 3.8
              trip = pickup // && smv.allowsTrip
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("conjunctively gate", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void SmvGateInBlockComment_CannotAuthorizeTrip()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i > 4
              dropout = i < 3.8
              trip = pickup /* && smv.allowsTrip */
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("conjunctively gate", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void SmvGateOutsideTripExpression_CannotAuthorizeTrip()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i > 4
              dropout = i < 3.8
              permission = smv.allowsTrip
              trip = pickup
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("conjunctively gate", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void TripOrBranch_CannotBypassSmvGate()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i > 4
              dropout = i < 3.8
              trip = pickup || smv.allowsTrip
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("OR (||)", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MultilineTripOrBranch_CannotBypassSmvGate()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i > 4
              dropout = i < 3.8
              trip = smv.allowsTrip
                || pickup
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("OR (||)", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MultilineConjunctiveTripGate_IsAccepted()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i >= setting("I>>")
              dropout = i < setting("I>>") * 0.95
              trip = pickup
                && smv.allowsTrip
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void TripEqualityComparison_DoesNotSatisfyTripAssignmentRequirement()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i > 4
              dropout = i < 3.8
              comparison = trip == pickup && smv.allowsTrip
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("trip expression is required", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void NegatedSmvGate_IsRejected()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i > 4
              dropout = i < 3.8
              trip = pickup && !smv.allowsTrip
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("conjunctively gate", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ComparedFalseSmvGate_IsRejected()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i > 4
              dropout = i < 3.8
              trip = pickup && smv.allowsTrip == false
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("conjunctively gate", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ParenthesizedSmvGate_IsAccepted()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i >= setting("I>>")
              dropout = i < setting("I>>") * 0.95
              trip = pickup && (smv.allowsTrip)
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void CommentedTripExpression_DoesNotSatisfyPolicy()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              dropout = i < 3.8
              // trip = i > 4 && smv.allowsTrip
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("trip expression is required", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ForbiddenCapabilityInsideComment_DoesNotRejectSafeSource()
    {
        const string source = """
            element "50P" {
              input i = IA.rms1c
              pickup = i >= setting("I>>")
              dropout = i < setting("I>>") * 0.95
              // File.ReadAllText("example-only")
              trip = pickup && smv.allowsTrip
            }
            """;

        var result = AlgorithmPolicyValidator.Validate(source);

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }
}
