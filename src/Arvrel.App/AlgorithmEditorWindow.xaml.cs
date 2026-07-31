using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Arvrel.Protection;
using Arvrel.Protection.Algorithms;

namespace Arvrel.App;

public partial class AlgorithmEditorWindow : Window
{
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

    private void ElementCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            LoadSources(resetCustom: true);
    }

    private void CopyStandard_Click(object sender, RoutedEventArgs e)
    {
        EditorText.Text = StandardSourceText.Text;
        ValidationText.Text = "Active standard source copied into the editable shadow workspace.";
        StatusText.Text = "Edit the copied source, validate it, then stage an immutable shadow definition.";
    }

    private void ResetTemplate_Click(object sender, RoutedEventArgs e) => LoadSources(resetCustom: true);

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var result = AlgorithmPolicyValidator.Validate(EditorText.Text);
        ValidationText.Text = Format(result);
        StatusText.Text = result.IsValid
            ? $"Validation passed · SHA-256 {result.ContentHash[..12]} · shadow only"
            : $"Validation failed with {result.Errors.Count} error(s).";
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

        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ARVREL", "algorithms");
        Directory.CreateDirectory(directory);
        var document = new
        {
            schemaVersion = 2,
            element = SelectedElement,
            stagedUtc = DateTimeOffset.UtcNow,
            sourceHash = result.ContentHash,
            activeSettings = new
            {
                _activeSettings.GroupName,
                _activeSettings.Revision,
                fingerprint = _activeSettings.Fingerprint()
            },
            mode = "deterministic-shadow-p1.1",
            activation = "not-active",
            outputBoundary = "virtual-only",
            source = EditorText.Text
        };
        var path = Path.Combine(directory, $"{SelectedElement}-{result.ContentHash[..12]}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        StatusText.Text = $"Shadow algorithm staged · {Path.GetFileName(path)} · active relay algorithm unchanged.";
    }

    private void LoadSources(bool resetCustom)
    {
        var source = AlgorithmSourceCatalog.Build(SelectedElement, _activeSettings);
        StandardSourceText.Text = source;
        if (resetCustom)
            EditorText.Text = source;
        ValidationText.Text = "Not validated. The active standard source remains read-only and executing.";
        StatusText.Text = $"Loaded exact active {SelectedElement} source from {_activeSettings.GroupName} revision {_activeSettings.Revision}.";
        CurveReferenceText.Text = BuildCurveReference();
    }

    private string BuildCurveReference()
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
        return SelectedElement == "50P-1"
            ? $"Definite time · {_activeSettings.PhaseInstantaneousDelay.TotalMilliseconds:0.###} ms"
            : $"Definite time · {_activeSettings.EarthInstantaneousDelay.TotalMilliseconds:0.###} ms";
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
            result.IsValid ? "PASS · deterministic shadow policy" : "FAIL",
            $"SHA-256: {result.ContentHash}"
        };
        lines.AddRange(result.Errors.Select(error => $"ERROR: {error}"));
        lines.AddRange(result.Warnings.Select(warning => $"WARN: {warning}"));
        if (result.IsValid)
        {
            lines.Add("INFO: Active settings remain separate from this source.");
            lines.Add("INFO: Staging does not activate or replace the running algorithm.");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
