using System.Globalization;
using System.Text;

namespace Arvrel.Protection;

public sealed record VirtualInjectionTableEntry(
    VirtualInjectionSignal Signal,
    bool Enabled,
    double Rms,
    double AngleDegrees);

public sealed record VirtualInjectionTableDocument(
    double? FrequencyHz,
    IReadOnlyList<VirtualInjectionTableEntry> Entries);

public static class VirtualInjectionTableText
{
    public const string Signature = "ARVREL-VIRTUAL-INJECTION\t1";

    public static string Serialize(VirtualInjectionTableDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateDocument(document);

        var entries = document.Entries.ToDictionary(entry => entry.Signal);
        var builder = new StringBuilder();
        builder.AppendLine(Signature);
        if (document.FrequencyHz is double frequency)
            builder.Append("FrequencyHz\t").AppendLine(Format(frequency));
        builder.AppendLine("Signal\tOn\tRMS value\tAngle (deg)\tUnit");

        foreach (var signal in Enum.GetValues<VirtualInjectionSignal>())
        {
            var entry = entries[signal];
            builder.Append(SignalLabel(signal)).Append('\t')
                .Append(entry.Enabled ? "1" : "0").Append('\t')
                .Append(Format(entry.Rms)).Append('\t')
                .Append(Format(VirtualInjectionChannel.NormalizeAngle(entry.AngleDegrees))).Append('\t')
                .Append(Unit(signal)).AppendLine();
        }

        return builder.ToString();
    }

    public static bool TryParse(
        string? text,
        out VirtualInjectionTableDocument? document,
        out string error)
    {
        document = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Clipboard does not contain a virtual-injection table.";
            return false;
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (lines.Count == 0)
        {
            error = "Clipboard does not contain a virtual-injection table.";
            return false;
        }

        double? frequencyHz = null;
        var headerIndex = -1;
        for (var index = 0; index < lines.Count; index++)
        {
            var cells = SplitCells(lines[index]);
            if (cells.Length >= 2 && HeaderKey(cells[0]) == "frequencyhz")
            {
                if (!TryParseNumber(cells[1], out var parsedFrequency) ||
                    !double.IsFinite(parsedFrequency) ||
                    parsedFrequency < VirtualInjectionProfile.MinimumFrequencyHz ||
                    parsedFrequency > VirtualInjectionProfile.MaximumFrequencyHz)
                {
                    error = $"Frequency must be {VirtualInjectionProfile.MinimumFrequencyHz:0}–{VirtualInjectionProfile.MaximumFrequencyHz:0} Hz.";
                    return false;
                }
                frequencyHz = parsedFrequency;
                continue;
            }

            if (cells.Any(cell => HeaderKey(cell) == "signal"))
            {
                headerIndex = index;
                break;
            }
        }

        if (headerIndex < 0)
        {
            error = "Table header must contain Signal, On, RMS value, and Angle columns.";
            return false;
        }

        var headers = SplitCells(lines[headerIndex]);
        var signalIndex = FindHeader(headers, "signal");
        var enabledIndex = FindHeader(headers, "on", "enabled");
        var rmsIndex = FindHeader(headers, "rmsvalue", "rms", "value");
        var angleIndex = FindHeader(headers, "angledeg", "angledegrees", "angle");
        if (signalIndex < 0 || enabledIndex < 0 || rmsIndex < 0 || angleIndex < 0)
        {
            error = "Table header must contain Signal, On, RMS value, and Angle columns.";
            return false;
        }

        var maximumIndex = new[] { signalIndex, enabledIndex, rmsIndex, angleIndex }.Max();
        var entries = new Dictionary<VirtualInjectionSignal, VirtualInjectionTableEntry>();
        for (var index = headerIndex + 1; index < lines.Count; index++)
        {
            var cells = SplitCells(lines[index]);
            if (cells.Length <= maximumIndex)
            {
                error = $"Table row {index - headerIndex} does not contain all required columns.";
                return false;
            }

            if (!TryParseSignal(cells[signalIndex], out var signal))
            {
                error = $"Unknown signal '{cells[signalIndex]}' in table row {index - headerIndex}.";
                return false;
            }
            if (entries.ContainsKey(signal))
            {
                error = $"Signal '{SignalLabel(signal)}' appears more than once.";
                return false;
            }
            if (!TryParseEnabled(cells[enabledIndex], out var enabled))
            {
                error = $"{SignalLabel(signal)}: On must be 1/0, true/false, on/off, or yes/no.";
                return false;
            }
            if (!TryParseNumber(cells[rmsIndex], out var rms) ||
                !double.IsFinite(rms) ||
                rms is < 0 or > 1_000_000_000)
            {
                error = $"{SignalLabel(signal)}: RMS value must be finite and between 0 and 1e9.";
                return false;
            }
            if (!TryParseNumber(cells[angleIndex], out var angle) || !double.IsFinite(angle))
            {
                error = $"{SignalLabel(signal)}: angle must be finite.";
                return false;
            }

            entries[signal] = new VirtualInjectionTableEntry(
                signal,
                enabled,
                rms,
                VirtualInjectionChannel.NormalizeAngle(angle));
        }

        var missing = Enum.GetValues<VirtualInjectionSignal>()
            .Where(signal => !entries.ContainsKey(signal))
            .Select(SignalLabel)
            .ToArray();
        if (missing.Length > 0)
        {
            error = $"Table is incomplete. Missing: {string.Join(", ", missing)}.";
            return false;
        }

        document = new VirtualInjectionTableDocument(
            frequencyHz,
            Enum.GetValues<VirtualInjectionSignal>().Select(signal => entries[signal]).ToArray());
        return true;
    }

    public static string SignalLabel(VirtualInjectionSignal signal) => signal switch
    {
        VirtualInjectionSignal.PhaseAVoltage => "V L1-E",
        VirtualInjectionSignal.PhaseBVoltage => "V L2-E",
        VirtualInjectionSignal.PhaseCVoltage => "V L3-E",
        VirtualInjectionSignal.NeutralVoltage => "V N",
        VirtualInjectionSignal.PhaseACurrent => "I L1",
        VirtualInjectionSignal.PhaseBCurrent => "I L2",
        VirtualInjectionSignal.PhaseCCurrent => "I L3",
        VirtualInjectionSignal.NeutralCurrent => "I N",
        _ => signal.ToString()
    };

    private static void ValidateDocument(VirtualInjectionTableDocument document)
    {
        ArgumentNullException.ThrowIfNull(document.Entries);
        if (document.FrequencyHz is double frequency &&
            (!double.IsFinite(frequency) ||
             frequency < VirtualInjectionProfile.MinimumFrequencyHz ||
             frequency > VirtualInjectionProfile.MaximumFrequencyHz))
            throw new ArgumentOutOfRangeException(nameof(document), "Frequency is outside the supported injection range.");

        var entries = document.Entries.ToDictionary(entry => entry.Signal);
        foreach (var signal in Enum.GetValues<VirtualInjectionSignal>())
        {
            if (!entries.TryGetValue(signal, out var entry))
                throw new ArgumentException($"Missing {SignalLabel(signal)}.", nameof(document));
            if (!double.IsFinite(entry.Rms) || entry.Rms is < 0 or > 1_000_000_000)
                throw new ArgumentOutOfRangeException(nameof(document), $"{SignalLabel(signal)} RMS is invalid.");
            if (!double.IsFinite(entry.AngleDegrees))
                throw new ArgumentOutOfRangeException(nameof(document), $"{SignalLabel(signal)} angle is invalid.");
        }
    }

    private static string[] SplitCells(string line)
        => line.Split('\t').Select(cell => cell.Trim()).ToArray();

    private static int FindHeader(IReadOnlyList<string> headers, params string[] accepted)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var key = HeaderKey(headers[index]);
            if (accepted.Contains(key, StringComparer.Ordinal))
                return index;
        }
        return -1;
    }

    private static string HeaderKey(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static bool TryParseSignal(string value, out VirtualInjectionSignal signal)
    {
        var key = HeaderKey(value);
        foreach (var candidate in Enum.GetValues<VirtualInjectionSignal>())
        {
            if (key == HeaderKey(candidate.ToString()) || key == HeaderKey(SignalLabel(candidate)))
            {
                signal = candidate;
                return true;
            }
        }

        signal = default;
        return false;
    }

    private static bool TryParseEnabled(string value, out bool enabled)
    {
        switch (HeaderKey(value))
        {
            case "1":
            case "true":
            case "on":
            case "yes":
            case "enabled":
                enabled = true;
                return true;
            case "0":
            case "false":
            case "off":
            case "no":
            case "disabled":
                enabled = false;
                return true;
            default:
                enabled = false;
                return false;
        }
    }

    private static bool TryParseNumber(string value, out double number)
    {
        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        return double.TryParse(value, styles, CultureInfo.InvariantCulture, out number) ||
               double.TryParse(value, styles, CultureInfo.CurrentCulture, out number);
    }

    private static string Unit(VirtualInjectionSignal signal)
        => signal <= VirtualInjectionSignal.NeutralVoltage ? "V" : "A";

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
