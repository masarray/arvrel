using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Arvrel.Protection.Algorithms;

namespace Arvrel.App;

public partial class AlgorithmEditorWindow : Window
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.Ordinal)
    {
        ["50P-1"] = """
            element "50P-1" {
              input phaseCurrent = max(IA.rms1c, IB.rms1c, IC.rms1c)
              pickup = phaseCurrent >= setting("I>>")
              dropout = phaseCurrent < setting("I>>") * setting("DropoutRatio")
              operate = pickup.persist(setting("Delay"))
              trip = operate && smv.allowsTrip
            }
            """,
        ["51P"] = """
            element "51P" {
              input phaseCurrent = max(IA.fundamental, IB.fundamental, IC.fundamental)
              multiple = phaseCurrent / setting("Is")
              operateTime = setting("TMS") * (0.14 / (pow(multiple, 0.02) - 1))
              progress = integrate(dt / operateTime) when multiple > 1
              dropout = multiple < setting("DropoutRatio")
              trip = progress >= 1 && smv.allowsTrip
            }
            """,
        ["50N"] = """
            element "50N" {
              input earthCurrent = current.residual
              pickup = earthCurrent >= setting("I0>>")
              dropout = earthCurrent < setting("I0>>") * setting("DropoutRatio")
              operate = pickup.persist(setting("Delay"))
              trip = operate && smv.allowsTrip
            }
            """,
        ["51N"] = """
            element "51N" {
              input earthCurrent = current.residual
              multiple = earthCurrent / setting("I0s")
              operateTime = setting("TMS") * (0.14 / (pow(multiple, 0.02) - 1))
              progress = integrate(dt / operateTime) when multiple > 1
              dropout = multiple < setting("DropoutRatio")
              trip = progress >= 1 && smv.allowsTrip
            }
            """
    };

    public AlgorithmEditorWindow()
    {
        InitializeComponent();
        LoadTemplate();
    }

    private string SelectedElement => ((ComboBoxItem)ElementCombo.SelectedItem).Content?.ToString() ?? "50P-1";

    private void ElementCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            LoadTemplate();
    }

    private void ResetTemplate_Click(object sender, RoutedEventArgs e) => LoadTemplate();

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var result = AlgorithmPolicyValidator.Validate(EditorText.Text);
        ValidationText.Text = Format(result);
        StatusText.Text = result.IsValid
            ? $"Validation passed · SHA-256 {result.ContentHash[..12]}"
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
            schemaVersion = 1,
            element = SelectedElement,
            stagedUtc = DateTimeOffset.UtcNow,
            hash = result.ContentHash,
            mode = "shadow-only-p0",
            source = EditorText.Text
        };
        var path = Path.Combine(directory, $"{SelectedElement}-{result.ContentHash[..12]}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        StatusText.Text = $"Algorithm staged as immutable shadow definition: {Path.GetFileName(path)}";
    }

    private void LoadTemplate()
    {
        EditorText.Text = _templates[SelectedElement];
        ValidationText.Text = "Not validated.";
        StatusText.Text = $"Loaded standard {SelectedElement} laboratory template.";
    }

    private static string Format(AlgorithmValidationResult result)
    {
        var lines = new List<string>
        {
            result.IsValid ? "PASS" : "FAIL",
            $"SHA-256: {result.ContentHash}"
        };
        lines.AddRange(result.Errors.Select(error => $"ERROR: {error}"));
        lines.AddRange(result.Warnings.Select(warning => $"WARN: {warning}"));
        return string.Join(Environment.NewLine, lines);
    }
}
