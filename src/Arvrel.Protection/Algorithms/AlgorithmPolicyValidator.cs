using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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

    private static readonly Regex TripAssignmentRegex = new(
        @"(?<![A-Za-z0-9_.])trip\s*=\s*(?<expression>[^\r\n}]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static AlgorithmValidationResult Validate(string source)
    {
        source ??= string.Empty;
        var errors = new List<string>();
        var warnings = new List<string>();
        var executableSource = StripComments(source);

        if (string.IsNullOrWhiteSpace(executableSource))
            errors.Add("Algorithm source is empty.");
        if (!ContainsKeyword(executableSource, "element"))
            errors.Add("An element declaration is required.");
        if (!ContainsTypedMeasurement(executableSource))
            errors.Add("At least one input, measurement, or phasor declaration is required.");

        ValidateTripExpressions(executableSource, errors);

        foreach (var token in ForbiddenTokens)
        {
            if (executableSource.Contains(token, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Forbidden capability or unbounded construct detected: {token}");
        }

        if (!HasBalancedBraces(executableSource))
            errors.Add("Braces are not balanced.");
        if (!ContainsKeyword(executableSource, "dropout"))
            warnings.Add("No explicit dropout expression was found; template runtime defaults will be used.");
        if (!executableSource.Contains("setting(", StringComparison.OrdinalIgnoreCase))
            warnings.Add("No external setting reference was found; review whether constants should be configurable.");
        if (executableSource.Contains("direction(", StringComparison.OrdinalIgnoreCase) &&
            !executableSource.Contains("polarizingAvailable", StringComparison.OrdinalIgnoreCase))
            warnings.Add("Directional source has no explicit polarizing-quantity supervision.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        return new AlgorithmValidationResult(errors.Count == 0, hash, errors, warnings);
    }

    private static void ValidateTripExpressions(string source, ICollection<string> errors)
    {
        var matches = TripAssignmentRegex.Matches(source);
        if (matches.Count == 0)
        {
            errors.Add("A trip expression is required.");
            return;
        }

        foreach (Match match in matches)
        {
            var expression = match.Groups["expression"].Value.Trim();
            if (string.IsNullOrWhiteSpace(expression))
            {
                errors.Add("Trip expression is empty.");
                continue;
            }

            // Until the research DSL has an AST-level boolean proof, disallow OR in the
            // final trip expression. This keeps the mandatory SMV permission as a true
            // conjunctive gate instead of a token that can be bypassed by another branch.
            if (expression.Contains("||", StringComparison.Ordinal))
            {
                errors.Add("Trip expression uses OR (||); Safe Laboratory Mode only accepts conjunctive trip logic so smv.allowsTrip cannot be bypassed.");
                continue;
            }

            var hasMandatoryGate = expression
                .Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(IsSmvTripGateTerm);

            if (!hasMandatoryGate)
                errors.Add("Safe Laboratory Mode requires every executable trip expression to conjunctively gate output with smv.allowsTrip.");
        }
    }

    private static bool IsSmvTripGateTerm(string term)
    {
        var candidate = term.Trim();
        while (candidate.Length >= 2 && candidate[0] == '(' && candidate[^1] == ')')
            candidate = candidate[1..^1].Trim();

        return string.Equals(candidate, "smv.allowsTrip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsTypedMeasurement(string source)
        => ContainsKeyword(source, "input") ||
           ContainsKeyword(source, "measurement") ||
           ContainsKeyword(source, "phasor");

    private static bool ContainsKeyword(string source, string keyword)
        => Regex.IsMatch(
            source,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(keyword)}(?![A-Za-z0-9_])",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static string StripComments(string source)
    {
        var builder = new StringBuilder(source.Length);
        var inString = false;
        var escaped = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inLineComment)
            {
                if (current is '\r' or '\n')
                {
                    inLineComment = false;
                    builder.Append(current);
                }
                else
                {
                    builder.Append(' ');
                }
                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    builder.Append("  ");
                    index++;
                    inBlockComment = false;
                }
                else
                {
                    builder.Append(current is '\r' or '\n' ? current : ' ');
                }
                continue;
            }

            if (inString)
            {
                builder.Append(current);
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (current == '"')
            {
                inString = true;
                builder.Append(current);
                continue;
            }

            if (current == '/' && next == '/')
            {
                builder.Append("  ");
                index++;
                inLineComment = true;
                continue;
            }

            if (current == '/' && next == '*')
            {
                builder.Append("  ");
                index++;
                inBlockComment = true;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool HasBalancedBraces(string value)
    {
        var balance = 0;
        var inString = false;
        var escaped = false;

        foreach (var character in value)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character == '{') balance++;
            if (character == '}') balance--;
            if (balance < 0) return false;
        }
        return balance == 0;
    }
}
