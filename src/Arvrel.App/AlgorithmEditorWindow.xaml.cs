using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Arvrel.Protection;
using Arvrel.Protection.Algorithms;

namespace Arvrel.App;

public partial class AlgorithmEditorWindow : Window
{
    private static readonly HashSet<string> ActivationCapableElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "50P-1", "51P", "50N", "51N"
    };

    private readonly ProtectionSettings _activeSettings;

    public AlgorithmEditorWindow() : this(new ProtectionSettings())
    {
    }

    public AlgorithmEditorWindow(ProtectionSettings activeSettings)
    {
        _activeSettings = activeSettings ?? throw new ArgumentNullException(nameof(activeSettings));
        _activeSettings.Validate();
        InitializeComponent();
        SettingsStatusText.Text = $"{_activeSettings.GroupName.ToUpperInvariant()} · REV {_activeSettings.Revision} · {_activeSettings.Fingerprint()[..12]}";
        LoadSources(resetCustom: true);
    }

    private string SelectedElement => ((ComboBoxItem)ElementCombo.SelectedItem).Content?.ToString() ?? "50P-1";
    private bool RuntimeActivationSupported => ActivationCapableElements.Contains(SelectedElement);

    private void ElementCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            LoadSources(resetCustom: true);
    }

    private void CopyStandard_Click(object sender, RoutedEventArgs e)
    {
        EditorText.Text = StandardSourceText.Text;
        ValidationText.Text = "Active standard source copied into the editable research workspace.";
        StatusText.Text = "Edit the copied source, compile it, run A/B, then stage the exact immutable definition.";
    }

    private void ResetTemplate_Click(object sender, RoutedEventArgs e) => LoadSources(resetCustom: true);

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var result = AlgorithmPolicyValidator.Validate(EditorText.Text);
        if (!result.IsValid)
        {
            ValidationText.Text = Format(result);
            StatusText.Text = $"Validation failed with {result.Errors.Count} error(s).";
            return;
        }

        if (!RuntimeActivationSupported)
        {
            ValidationText.Text = Format(result) + Environment.NewLine + "INFO: This P2 feeder element remains validation/shadow-only; activation is restricted to P1.1 50P-1/51P/50N/51N.";
            StatusText.Text = $"Policy validation passed · SHA-256 {result.ContentHash[..12]} · feeder shadow-only";
            return;
        }

        try
        {
            var compiled = AlgorithmSandboxCompiler.Compile(SelectedElement, EditorText.Text, _activeSettings);
            ValidationText.Text = Format(result) + Environment.NewLine +
                                  $"COMPILE: PASS · {compiled.InstructionCost}/{AlgorithmSandboxCompiler.MaximumInstructionsPerFrame} instruction budget" + Environment.NewLine +
                                  $"LIMITS: {AlgorithmSandboxCompiler.MaximumSourceBytes / 1024} KiB source · {AlgorithmSandboxCompiler.MaximumStatements} statements · 64 runtime variables";
            StatusText.Text = $"Typed deterministic compile passed · SHA-256 {compiled.SourceHash[..12]} · virtual output only";
        }
        catch (AlgorithmCompilationException ex)
        {
            ValidationText.Text = Format(result) + Environment.NewLine + $"COMPILE ERROR: {ex.Message}";
            StatusText.Text = "Typed deterministic compile failed.";
        }
    }

    private void TestBench_Click(object sender, RoutedEventArgs e)
    {
        if (!RuntimeActivationSupported)
        {
            StatusText.Text = "A/B runtime test is currently restricted to P1.1 50P-1/51P/50N/51N elements.";
            return;
        }

        try
        {
            var result = AlgorithmTestBench.Compare(
                SelectedElement,
                StandardSourceText.Text,
                EditorText.Text,
                _activeSettings);
            ValidationText.Text =
                $"A/B TEST: PASS · {result.Frames} deterministic frames" + Environment.NewLine +
                $"DIVERGENT FRAMES: {result.DivergentFrames}" + Environment.NewLine +
                $"STANDARD PICKUP/TRIP: {FormatTime(result.StandardFirstPickup)} / {FormatTime(result.StandardFirstTrip)}" + Environment.NewLine +
                $"CUSTOM PICKUP/TRIP:   {FormatTime(result.CustomFirstPickup)} / {FormatTime(result.CustomFirstTrip)}" + Environment.NewLine +
                $"CUSTOM FINAL: {result.CustomFinal.Snapshot.State} · trip {result.CustomFinal.TripRequested}";
            StatusText.Text = result.DivergentFrames == 0
                ? "A/B deterministic scenario matched the standard reference on every frame."
                : $"A/B completed · {result.DivergentFrames} divergent frame(s) recorded for engineering review.";
        }
        catch (AlgorithmCompilationException ex)
        {
            ValidationText.Text = $"A/B TEST: FAIL TO COMPILE{Environment.NewLine}{ex.Message}";
            StatusText.Text = "A/B test rejected because the custom source did not pass deterministic compile.";
        }
    }

    private void Stage_Click(object sender, RoutedEventArgs e)
    {
        var result = AlgorithmPolicyValidator.Validate(EditorText.Text);
        ValidationText.Text = Format(result);
        if (!result.IsValid)
        {
            StatusText.Text = "Staging rejected. Resolve validation errors first.";
            return;
        }

        AlgorithmDefinitionIdentity? runtimeIdentity = null;
        if (RuntimeActivationSupported)
        {
            try
            {
                runtimeIdentity = AlgorithmRuntimeRegistry.Stage(
                    SelectedElement,
                    EditorText.Text,
                    _activeSettings,
                    AuthorNoteText.Text);
            }
            catch (AlgorithmCompilationException ex)
            {
                ValidationText.Text += Environment.NewLine + $"COMPILE ERROR: {ex.Message}";
                StatusText.Text = "Staging rejected by typed deterministic compile.";
                return;
            }
        }

        var directory = AlgorithmDirectory();
        Directory.CreateDirectory(directory);
        var settingsFingerprint = _activeSettings.Fingerprint();
        var document = new
        {
            schemaVersion = 4,
            element = SelectedElement,
            stagedUtc = runtimeIdentity?.StagedUtc ?? DateTimeOffset.UtcNow,
            version = runtimeIdentity?.Version ?? $"shadow-{result.ContentHash[..12]}",
            sourceHash = result.ContentHash,
            authorNote = NormalizeNote(AuthorNoteText.Text),
            activeSettings = new
            {
                _activeSettings.GroupName,
                _activeSettings.Revision,
                fingerprint = settingsFingerprint
            },
            mode = RuntimeActivationSupported ? "deterministic-dual-mode" : "deterministic-shadow-only",
            activation = "staged-not-active",
            outputBoundary = "virtual-only",
            source = EditorText.Text
        };
        var path = Path.Combine(
            directory,
            $"{SelectedElement}-{settingsFingerprint[..12]}-{result.ContentHash[..12]}.json");
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });

        try
        {
            WriteDurableTemporaryFile(temporaryPath, json);
            try
            {
                File.Move(temporaryPath, path, overwrite: false);
                StatusText.Text = RuntimeActivationSupported
                    ? $"Custom definition staged · {Path.GetFileName(path)} · standard remains active, custom runs shadow."
                    : $"Feeder shadow staged · {Path.GetFileName(path)} · activation not supported for this P2 element.";
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Delete(temporaryPath);
                StatusText.Text = $"Immutable definition already staged · {Path.GetFileName(path)} · evidence was not overwritten.";
            }
            UpdateRuntimeBadge();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporaryPath);
            StatusText.Text = $"Shadow staging artifact failed · {ex.Message}. Runtime staging remains governed by the in-memory exact hash.";
        }
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (!RuntimeActivationSupported)
        {
            StatusText.Text = "Activation is restricted to P1.1 50P-1/51P/50N/51N. This feeder element remains shadow-only.";
            return;
        }

        var validation = AlgorithmPolicyValidator.Validate(EditorText.Text);
        if (!validation.IsValid)
        {
            ValidationText.Text = Format(validation);
            StatusText.Text = "Activation rejected. Validate and stage the exact source first.";
            return;
        }

        var staged = AlgorithmRuntimeRegistry.Snapshot().Staged.FirstOrDefault(identity =>
            string.Equals(identity.Element, SelectedElement, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(identity.SourceHash, validation.ContentHash, StringComparison.OrdinalIgnoreCase));
        if (staged is null)
        {
            StatusText.Text = "Activation rejected. The exact current source hash is not staged; use Stage first.";
            return;
        }

        try
        {
            var active = AlgorithmRuntimeRegistry.Activate(
                SelectedElement,
                staged.SourceHash,
                _activeSettings,
                AuthorNoteText.Text);
            AppendActivationAudit("ACTIVATE", active);
            ValidationText.Text =
                $"ACTIVATE: PASS{Environment.NewLine}" +
                $"ELEMENT: {active.Element}{Environment.NewLine}" +
                $"VERSION: {active.Version}{Environment.NewLine}" +
                $"SHA-256: {active.SourceHash}{Environment.NewLine}" +
                $"SETTINGS: {active.SettingsFingerprint}{Environment.NewLine}" +
                "BOUNDARY: VIRTUAL OUTPUT ONLY · native standard remains parallel shadow reference";
            StatusText.Text = $"CUSTOM ACTIVE · {active.Element} · {active.Version} · VIRTUAL OUTPUT ONLY";
            UpdateRuntimeBadge();
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = $"Activation rejected · {ex.Message}";
        }
    }

    private void Rollback_Click(object sender, RoutedEventArgs e)
    {
        if (!RuntimeActivationSupported)
        {
            StatusText.Text = "No executable custom runtime exists for this feeder shadow element.";
            return;
        }

        var before = AlgorithmRuntimeRegistry.Snapshot().Active.FirstOrDefault(identity =>
            string.Equals(identity.Element, SelectedElement, StringComparison.OrdinalIgnoreCase));
        if (before is null)
        {
            StatusText.Text = $"{SelectedElement} is already using the native standard algorithm.";
            UpdateRuntimeBadge();
            return;
        }

        var restored = AlgorithmRuntimeRegistry.RollbackElement(SelectedElement, AuthorNoteText.Text);
        AppendActivationAudit("ROLLBACK", restored ?? before with
        {
            Version = "standard-native",
            SourceHash = AlgorithmPolicyValidator.Validate(StandardSourceText.Text).ContentHash,
            ActivatedUtc = DateTimeOffset.UtcNow,
            AuthorNote = NormalizeNote(AuthorNoteText.Text)
        });
        ValidationText.Text = restored is null
            ? $"ROLLBACK: PASS · {SelectedElement} restored to native standard execution."
            : $"ROLLBACK: PASS · restored {restored.Version} · {restored.SourceHash}.";
        StatusText.Text = restored is null
            ? $"STANDARD ACTIVE · {SelectedElement} · custom remains available as staged shadow evidence."
            : $"CUSTOM ACTIVE · restored {restored.Version} · VIRTUAL OUTPUT ONLY";
        UpdateRuntimeBadge();
    }

    private static void WriteDurableTemporaryFile(string path, string content)
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16_384,
            FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void AppendActivationAudit(string action, AlgorithmDefinitionIdentity identity)
    {
        var directory = AlgorithmDirectory();
        Directory.CreateDirectory(directory);
        var record = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            timestamp = DateTimeOffset.UtcNow,
            action,
            identity.Element,
            identity.Version,
            identity.SourceHash,
            identity.SettingsFingerprint,
            identity.AuthorNote,
            identity.OutputBoundary
        }) + Environment.NewLine;
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(record);
        using var stream = new FileStream(
            Path.Combine(directory, "activation-audit.jsonl"),
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static string AlgorithmDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ARVREL", "algorithms");

    private static string NormalizeNote(string? value)
    {
        var note = string.IsNullOrWhiteSpace(value) ? "No author note supplied." : value.Trim();
        return note.Length <= 240 ? note : note[..240];
    }

    private static string FormatTime(TimeSpan? value)
        => value.HasValue ? $"{value.Value.TotalMilliseconds:0.###} ms" : "not reached";

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only. The hidden temporary file is never presented
            // as staged evidence and has a unique name for later housekeeping.
        }
    }

    private void LoadSources(bool resetCustom)
    {
        var source = AlgorithmSourceCatalog.Build(SelectedElement, _activeSettings);
        StandardSourceText.Text = source;
        if (resetCustom)
            EditorText.Text = source;
        ValidationText.Text = RuntimeActivationSupported
            ? "Not compiled. Standard executes natively; staged custom runs deterministic shadow until explicit Activate."
            : "Not validated. This P2 feeder source is exposed for deterministic shadow research only.";
        StatusText.Text = $"Loaded exact active {SelectedElement} standard source from {_activeSettings.GroupName} revision {_activeSettings.Revision}.";
        CurveReferenceText.Text = BuildElementReference();
        UpdateRuntimeBadge();
    }

    private void UpdateRuntimeBadge()
    {
        if (CustomModeBadgeText is null)
            return;
        var snapshot = AlgorithmRuntimeRegistry.Snapshot();
        var active = snapshot.Active.FirstOrDefault(identity => string.Equals(identity.Element, SelectedElement, StringComparison.OrdinalIgnoreCase));
        var staged = snapshot.Staged.FirstOrDefault(identity => string.Equals(identity.Element, SelectedElement, StringComparison.OrdinalIgnoreCase));
        CustomModeBadgeText.Text = active is not null
            ? "CUSTOM ACTIVE · VIRTUAL"
            : staged is not null
                ? "SHADOW RUNNING"
                : RuntimeActivationSupported ? "NOT STAGED" : "SHADOW ONLY";
    }

    private string BuildElementReference()
    {
        if (SelectedElement == "51P")
            return CurveReference(
                _activeSettings.PhaseTimeCurve,
                _activeSettings.PhaseTimeMultiplier,
                _activeSettings.PhaseTimeDefiniteDelay,
                _activeSettings.PhaseTimeMinimumOperateTime,
                _activeSettings.PhaseTimeUserK,
                _activeSettings.PhaseTimeUserAlpha,
                _activeSettings.PhaseTimeUserC);
        if (SelectedElement == "51N")
            return CurveReference(
                _activeSettings.EarthTimeCurve,
                _activeSettings.EarthTimeMultiplier,
                _activeSettings.EarthTimeDefiniteDelay,
                _activeSettings.EarthTimeMinimumOperateTime,
                _activeSettings.EarthTimeUserK,
                _activeSettings.EarthTimeUserAlpha,
                _activeSettings.EarthTimeUserC);

        var feeder = _activeSettings.Feeder;
        return SelectedElement switch
        {
            "50P-1" => $"Definite time · {_activeSettings.PhaseInstantaneousDelay.TotalMilliseconds:0.###} ms",
            "50N" => $"Definite time · {_activeSettings.EarthInstantaneousDelay.TotalMilliseconds:0.###} ms",
            "67P" =>
                $"{(feeder.DirectionalPhase67Enabled ? "ENABLED" : "DISABLED")} · V1/I1 polarization\n" +
                $"{feeder.DirectionalPhase67Sense} · MTA {feeder.DirectionalPhase67CharacteristicAngleDeg:0.###}°\n" +
                $"I> {feeder.DirectionalPhase67PickupA:0.###} A · V1 min {feeder.DirectionalPhase67MinimumPolarizingVoltageV:0.###} V\n" +
                $"Delay {feeder.DirectionalPhase67Delay.TotalMilliseconds:0.###} ms",
            "67N" =>
                $"{(feeder.DirectionalEarth67NEnabled ? "ENABLED" : "DISABLED")} · 3V0/3I0 polarization\n" +
                $"{feeder.DirectionalEarth67NSense} · MTA {feeder.DirectionalEarth67NCharacteristicAngleDeg:0.###}°\n" +
                $"3I0> {feeder.DirectionalEarth67NPickupA:0.###} A · 3V0 min {feeder.DirectionalEarth67NMinimumPolarizingVoltageV:0.###} V\n" +
                $"Delay {feeder.DirectionalEarth67NDelay.TotalMilliseconds:0.###} ms",
            "27" =>
                $"{(feeder.Undervoltage27Enabled ? "ENABLED" : "DISABLED")} · {feeder.Undervoltage27Mode}\n" +
                $"{feeder.Undervoltage27Logic} · V< {feeder.Undervoltage27PickupV:0.###} V\n" +
                $"Delay {feeder.Undervoltage27Delay.TotalMilliseconds:0.###} ms · reset ×{feeder.Undervoltage27ResetRatio:0.###}",
            "59" =>
                $"{(feeder.Overvoltage59Enabled ? "ENABLED" : "DISABLED")} · {feeder.Overvoltage59Mode}\n" +
                $"{feeder.Overvoltage59Logic} · V> {feeder.Overvoltage59PickupV:0.###} V\n" +
                $"Delay {feeder.Overvoltage59Delay.TotalMilliseconds:0.###} ms · dropout ×{feeder.Overvoltage59DropoutRatio:0.###}",
            "59N" =>
                $"{(feeder.ResidualOvervoltage59NEnabled ? "ENABLED" : "DISABLED")} · residual 3V0\n" +
                $"3V0> {feeder.ResidualOvervoltage59NPickupV:0.###} V\n" +
                $"Delay {feeder.ResidualOvervoltage59NDelay.TotalMilliseconds:0.###} ms · dropout ×{feeder.ResidualOvervoltage59NDropoutRatio:0.###}",
            _ => "No element reference available."
        };
    }

    private static string CurveReference(
        IecCurveFamily curve,
        double tms,
        TimeSpan definite,
        TimeSpan minimum,
        double k,
        double alpha,
        double c)
    {
        var formula = IecCurveCalculator.Formula(curve, k, alpha, c);
        var at2 = IecCurveCalculator.GetOperateTimeSeconds(curve, 2, tms, definite, minimum, k, alpha, c);
        var at5 = IecCurveCalculator.GetOperateTimeSeconds(curve, 5, tms, definite, minimum, k, alpha, c);
        var at10 = IecCurveCalculator.GetOperateTimeSeconds(curve, 10, tms, definite, minimum, k, alpha, c);
        return $"{curve}\n{formula}\n2× {at2:0.###} s · 5× {at5:0.###} s · 10× {at10:0.###} s";
    }

    private static string Format(AlgorithmValidationResult result)
    {
        var lines = new List<string>
        {
            result.IsValid ? "PASS · deterministic research policy" : "FAIL",
            $"SHA-256: {result.ContentHash}"
        };
        lines.AddRange(result.Errors.Select(error => $"ERROR: {error}"));
        lines.AddRange(result.Warnings.Select(warning => $"WARN: {warning}"));
        if (result.IsValid)
        {
            lines.Add("INFO: Active settings remain separate from this source.");
            lines.Add("INFO: Stage is shadow-only; Activate is an explicit second action.");
            lines.Add("INFO: Runtime output boundary remains virtual-only.");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
