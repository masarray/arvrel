using System.Security.Cryptography;
using System.Text;

namespace Arvrel.Protection.Algorithms;

public sealed record AlgorithmValidationResult(
    bool IsValid,
    string ContentHash,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public static class AlgorithmPolicyValidator
{
    private static readonly string[] ForbiddenTokens =
    {
        "System.IO",
        "File.",
        "Directory.",
        "HttpClient",
        "Socket",
        "Process.",
        "DllImport",
        "Marshal.",
        "unsafe",
        "while(true)",
        "while (true)",
        "for(;;)",
        "for (;;)",
        "Reflection",
        "Environment.Exit"
    };

    public static AlgorithmValidationResult Validate(string source)
    {
        source ??= string.Empty;
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(source))
            errors.Add("Algorithm source is empty.");
        if (!source.Contains("smv.allowsTrip", StringComparison.Ordinal))
            errors.Add("Safe Laboratory Mode requires the final trip expression to include smv.allowsTrip.");
        if (!source.Contains("element", StringComparison.OrdinalIgnoreCase))
            errors.Add("An element declaration is required.");
        if (!ContainsTypedMeasurement(source))
            errors.Add("At least one input, measurement, or phasor declaration is required.");
        if (!source.Contains("trip", StringComparison.OrdinalIgnoreCase))
            errors.Add("A trip expression is required.");

        foreach (var token in ForbiddenTokens)
        {
            if (source.Contains(token, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Forbidden capability or unbounded construct detected: {token}");
        }

        if (!HasBalancedBraces(source))
            errors.Add("Braces are not balanced.");
        if (!source.Contains("dropout", StringComparison.OrdinalIgnoreCase))
            warnings.Add("No explicit dropout expression was found; template runtime defaults will be used.");
        if (!source.Contains("setting(", StringComparison.OrdinalIgnoreCase))
            warnings.Add("No external setting reference was found; review whether constants should be configurable.");
        if (source.Contains("direction(", StringComparison.OrdinalIgnoreCase) &&
            !source.Contains("polarizingAvailable", StringComparison.OrdinalIgnoreCase))
            warnings.Add("Directional source has no explicit polarizing-quantity supervision.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        return new AlgorithmValidationResult(errors.Count == 0, hash, errors, warnings);
    }

    private static bool ContainsTypedMeasurement(string source)
        => source.Contains("input", StringComparison.OrdinalIgnoreCase) ||
           source.Contains("measurement", StringComparison.OrdinalIgnoreCase) ||
           source.Contains("phasor", StringComparison.OrdinalIgnoreCase);

    private static bool HasBalancedBraces(string value)
    {
        var balance = 0;
        foreach (var character in value)
        {
            if (character == '{') balance++;
            if (character == '}') balance--;
            if (balance < 0) return false;
        }
        return balance == 0;
    }
}
