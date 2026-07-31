using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Arvrel.ProcessBus;
using Arvrel.Protection;
using Microsoft.Win32;

namespace Arvrel.App;

public partial class ProtectionSettingsWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SmvMeasurementContext _measurementContext;
    private bool _loading;

    public ProtectionSettingsWindow(ProtectionSettings settings, SmvMeasurementContext measurementContext)
    {
        InitializeComponent();
        _measurementContext = measurementContext;
        Phase51CurveCombo.ItemsSource = CurveOptions;
        Earth51CurveCombo.ItemsSource = CurveOptions;
        Phase51ResetCombo.ItemsSource = ResetOptions;
        Earth51ResetCombo.ItemsSource = ResetOptions;
        LoadSettings(settings);
    }

    public ProtectionSettings? Result { get; private set; }

    private static IReadOnlyList<EnumOption<IecCurveFamily>> CurveOptions { get; } = new[]
    {
        new EnumOption<IecCurveFamily>(IecCurveFamily.StandardInverse, "IEC Standard / Normal Inverse"),
        new EnumOption<IecCurveFamily>(IecCurveFamily.VeryInverse, "IEC Very Inverse"),
        new EnumOption<IecCurveFamily>(IecCurveFamily.ExtremelyInverse, "IEC Extremely Inverse"),
        new EnumOption<IecCurveFamily>(IecCurveFamily.LongTimeInverse, "IEC Long-Time Inverse"),
        new EnumOption<IecCurveFamily>(IecCurveFamily.DefiniteTime, "Definite Time"),
        new EnumOption<IecCurveFamily>(IecCurveFamily.UserDefined, "User-defined IEC-form curve")
    };

    private static IReadOnlyList<EnumOption<ProtectionResetMode>> ResetOptions { get; } = new[]
    {
        new EnumOption<ProtectionResetMode>(ProtectionResetMode.Instantaneous, "Instantaneous reset"),
        new EnumOption<ProtectionResetMode>(ProtectionResetMode.DefiniteTime, "Definite-time reset"),
        new EnumOption<ProtectionResetMode>(ProtectionResetMode.InverseMemory, "Inverse memory reset")
    };

    private void LoadSettings(ProtectionSettings settings)
    {
        _loading = true;
        try
        {
            GroupNameText.Text = settings.GroupName;
            RevisionText.Text = settings.Revision.ToString(CultureInfo.InvariantCulture);
            CtContextText.Text = $"{_measurementContext.CtPrimaryA:0.###}/{_measurementContext.CtSecondaryA:0.###} A · {_measurementContext.NominalFrequencyHz:0.###} Hz";

            Phase50Enabled.IsChecked = settings.PhaseInstantaneousEnabled;
            Phase50PickupText.Text = Format(settings.PhaseInstantaneousPickupA);
            Phase50DelayText.Text = FormatTime(settings.PhaseInstantaneousDelay);
            Phase50DropoutText.Text = Format(settings.PhaseInstantaneousDropoutRatio);

            Phase51Enabled.IsChecked = settings.PhaseTimeEnabled;
            Phase51PickupText.Text = Format(settings.PhaseTimePickupA);
            Select(Phase51CurveCombo, settings.PhaseTimeCurve);
            Phase51TmsText.Text = Format(settings.PhaseTimeMultiplier);
            Phase51DefiniteText.Text = FormatTime(settings.PhaseTimeDefiniteDelay);
            Phase51MinimumText.Text = FormatTime(settings.PhaseTimeMinimumOperateTime);
            Phase51DropoutText.Text = Format(settings.PhaseTimeDropoutRatio);
            Select(Phase51ResetCombo, settings.PhaseTimeResetMode);
            Phase51ResetTimeText.Text = FormatTime(settings.PhaseTimeResetDelay);
            Phase51KText.Text = Format(settings.PhaseTimeUserK);
            Phase51AlphaText.Text = Format(settings.PhaseTimeUserAlpha);
            Phase51CText.Text = Format(settings.PhaseTimeUserC);

            Earth50Enabled.IsChecked = settings.EarthInstantaneousEnabled;
            Earth50PickupText.Text = Format(settings.EarthInstantaneousPickupA);
            Earth50DelayText.Text = FormatTime(settings.EarthInstantaneousDelay);
            Earth50DropoutText.Text = Format(settings.EarthInstantaneousDropoutRatio);

            Earth51Enabled.IsChecked = settings.EarthTimeEnabled;
            Earth51PickupText.Text = Format(settings.EarthTimePickupA);
            Select(Earth51CurveCombo, settings.EarthTimeCurve);
            Earth51TmsText.Text = Format(settings.EarthTimeMultiplier);
            Earth51DefiniteText.Text = FormatTime(settings.EarthTimeDefiniteDelay);
            Earth51MinimumText.Text = FormatTime(settings.EarthTimeMinimumOperateTime);
            Earth51DropoutText.Text = Format(settings.EarthTimeDropoutRatio);
            Select(Earth51ResetCombo, settings.EarthTimeResetMode);
            Earth51ResetTimeText.Text = FormatTime(settings.EarthTimeResetDelay);
            Earth51KText.Text = Format(settings.EarthTimeUserK);
            Earth51AlphaText.Text = Format(settings.EarthTimeUserAlpha);
            Earth51CText.Text = Format(settings.EarthTimeUserC);
        }
        finally
        {
            _loading = false;
        }
        UpdatePreviews();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildSettings(out var settings, out var error))
        {
            ValidationText.Text = error;
            MessageBox.Show(this, error, "Invalid protection settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = settings;
        DialogResult = true;
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        LoadSettings(new ProtectionSettings());
        ValidationText.Text = "Factory laboratory defaults restored in the editor. Select Apply settings to activate them.";
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildSettings(out var settings, out var error))
        {
            ValidationText.Text = error;
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save ARVREL protection setting group",
            Filter = "ARVREL protection settings (*.arvsettings)|*.arvsettings|JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"{Sanitize(settings.GroupName)}-rev{settings.Revision}.arvsettings"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(settings, JsonOptions));
        ValidationText.Text = $"Preset saved · {Path.GetFileName(dialog.FileName)} · {settings.Fingerprint()[..12]}";
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load ARVREL protection setting group",
            Filter = "ARVREL protection settings (*.arvsettings;*.json)|*.arvsettings;*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var settings = JsonSerializer.Deserialize<ProtectionSettings>(File.ReadAllText(dialog.FileName), JsonOptions)
                           ?? throw new InvalidDataException("The preset does not contain protection settings.");
            settings.Validate();
            LoadSettings(settings);
            ValidationText.Text = $"Preset loaded · {Path.GetFileName(dialog.FileName)}. Select Apply settings to activate it.";
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "Preset load failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GeneralField_Changed(object sender, RoutedEventArgs e) => UpdatePreviews();
    private void AnySetting_Changed(object sender, RoutedEventArgs e) => UpdatePreviews();

    private void UpdatePreviews()
    {
        if (_loading || !IsLoaded)
            return;

        if (!TryBuildSettings(out var settings, out var error))
        {
            ValidationText.Text = error;
            return;
        }

        HeaderGroupText.Text = $"{settings.GroupName.ToUpperInvariant()} · REV {settings.Revision}";
        HeaderFingerprintText.Text = $"SETTINGS {settings.Fingerprint()[..12]}";
        Phase50EquivalentText.Text = Equivalent("I>>", settings.PhaseInstantaneousPickupA);
        Phase51EquivalentText.Text = Equivalent("Is", settings.PhaseTimePickupA);
        Earth50EquivalentText.Text = Equivalent("I0>>", settings.EarthInstantaneousPickupA);
        Earth51EquivalentText.Text = Equivalent("I0s", settings.EarthTimePickupA);
        Phase51PreviewText.Text = BuildPreview(
            settings.PhaseTimeCurve,
            settings.PhaseTimeMultiplier,
            settings.PhaseTimeDefiniteDelay,
            settings.PhaseTimeMinimumOperateTime,
            settings.PhaseTimeUserK,
            settings.PhaseTimeUserAlpha,
            settings.PhaseTimeUserC);
        Earth51PreviewText.Text = BuildPreview(
            settings.EarthTimeCurve,
            settings.EarthTimeMultiplier,
            settings.EarthTimeDefiniteDelay,
            settings.EarthTimeMinimumOperateTime,
            settings.EarthTimeUserK,
            settings.EarthTimeUserAlpha,
            settings.EarthTimeUserC);
        ValidationText.Text = "Settings valid · Apply will reset protection timers and the virtual trip latch.";
    }

    private bool TryBuildSettings(out ProtectionSettings settings, out string error)
    {
        try
        {
            settings = new ProtectionSettings
            {
                GroupName = string.IsNullOrWhiteSpace(GroupNameText.Text) ? "GROUP A" : GroupNameText.Text.Trim(),
                Revision = ParseInt(RevisionText.Text, "Revision"),

                PhaseInstantaneousEnabled = Phase50Enabled.IsChecked == true,
                PhaseInstantaneousPickupA = ParseDouble(Phase50PickupText.Text, "50P pickup"),
                PhaseInstantaneousDelay = ParseTime(Phase50DelayText.Text, "50P delay"),
                PhaseInstantaneousDropoutRatio = ParseDouble(Phase50DropoutText.Text, "50P dropout ratio"),

                PhaseTimeEnabled = Phase51Enabled.IsChecked == true,
                PhaseTimePickupA = ParseDouble(Phase51PickupText.Text, "51P pickup"),
                PhaseTimeCurve = Selected<IecCurveFamily>(Phase51CurveCombo, "51P characteristic"),
                PhaseTimeMultiplier = ParseDouble(Phase51TmsText.Text, "51P TMS"),
                PhaseTimeDefiniteDelay = ParseTime(Phase51DefiniteText.Text, "51P definite delay"),
                PhaseTimeMinimumOperateTime = ParseTime(Phase51MinimumText.Text, "51P minimum operate time", allowZero: true),
                PhaseTimeDropoutRatio = ParseDouble(Phase51DropoutText.Text, "51P dropout ratio"),
                PhaseTimeResetMode = Selected<ProtectionResetMode>(Phase51ResetCombo, "51P reset mode"),
                PhaseTimeResetDelay = ParseTime(Phase51ResetTimeText.Text, "51P reset time"),
                PhaseTimeUserK = ParseDouble(Phase51KText.Text, "51P user K"),
                PhaseTimeUserAlpha = ParseDouble(Phase51AlphaText.Text, "51P user alpha"),
                PhaseTimeUserC = ParseDouble(Phase51CText.Text, "51P user C", allowZero: true),

                EarthInstantaneousEnabled = Earth50Enabled.IsChecked == true,
                EarthInstantaneousPickupA = ParseDouble(Earth50PickupText.Text, "50N pickup"),
                EarthInstantaneousDelay = ParseTime(Earth50DelayText.Text, "50N delay"),
                EarthInstantaneousDropoutRatio = ParseDouble(Earth50DropoutText.Text, "50N dropout ratio"),

                EarthTimeEnabled = Earth51Enabled.IsChecked == true,
                EarthTimePickupA = ParseDouble(Earth51PickupText.Text, "51N pickup"),
                EarthTimeCurve = Selected<IecCurveFamily>(Earth51CurveCombo, "51N characteristic"),
                EarthTimeMultiplier = ParseDouble(Earth51TmsText.Text, "51N TMS"),
                EarthTimeDefiniteDelay = ParseTime(Earth51DefiniteText.Text, "51N definite delay"),
                EarthTimeMinimumOperateTime = ParseTime(Earth51MinimumText.Text, "51N minimum operate time", allowZero: true),
                EarthTimeDropoutRatio = ParseDouble(Earth51DropoutText.Text, "51N dropout ratio"),
                EarthTimeResetMode = Selected<ProtectionResetMode>(Earth51ResetCombo, "51N reset mode"),
                EarthTimeResetDelay = ParseTime(Earth51ResetTimeText.Text, "51N reset time"),
                EarthTimeUserK = ParseDouble(Earth51KText.Text, "51N user K"),
                EarthTimeUserAlpha = ParseDouble(Earth51AlphaText.Text, "51N user alpha"),
                EarthTimeUserC = ParseDouble(Earth51CText.Text, "51N user C", allowZero: true)
            };
            settings.Validate();
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            settings = new ProtectionSettings();
            error = ex.Message;
            return false;
        }
    }

    private string Equivalent(string symbol, double secondary)
    {
        var primary = secondary * _measurementContext.PrimaryRatio;
        return $"{symbol} {secondary:0.###} A secondary  →  {primary:0.###} A primary";
    }

    private static string BuildPreview(
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
        return $"{formula}\n2× = {at2:0.###} s   ·   5× = {at5:0.###} s   ·   10× = {at10:0.###} s";
    }

    private static double ParseDouble(string text, string name, bool allowZero = false)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            throw new FormatException($"{name} must be a number.");
        if (!double.IsFinite(value) || (allowZero ? value < 0 : value <= 0))
            throw new FormatException($"{name} must be {(allowZero ? "zero or greater" : "greater than zero")}.");
        return value;
    }

    private static int ParseInt(string text, string name)
    {
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 1)
            throw new FormatException($"{name} must be an integer of 1 or greater.");
        return value;
    }

    private static TimeSpan ParseTime(string text, string name, bool allowZero = false)
    {
        var normalized = text.Trim().ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
        double multiplier;
        if (normalized.EndsWith("ms", StringComparison.Ordinal))
        {
            multiplier = 1;
            normalized = normalized[..^2];
        }
        else if (normalized.EndsWith('s'))
        {
            multiplier = 1000;
            normalized = normalized[..^1];
        }
        else
        {
            multiplier = 1;
        }

        var milliseconds = ParseDouble(normalized, name, allowZero) * multiplier;
        if (milliseconds > TimeSpan.FromMinutes(10).TotalMilliseconds)
            throw new FormatException($"{name} must not exceed 10 minutes.");
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static T Selected<T>(System.Windows.Controls.ComboBox combo, string name) where T : struct, Enum
    {
        if (combo.SelectedItem is EnumOption<T> option)
            return option.Value;
        throw new FormatException($"{name} must be selected.");
    }

    private static void Select<T>(System.Windows.Controls.ComboBox combo, T value) where T : struct, Enum
        => combo.SelectedItem = ((IEnumerable<EnumOption<T>>)combo.ItemsSource).First(option => EqualityComparer<T>.Default.Equals(option.Value, value));

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string FormatTime(TimeSpan value) => value.TotalMilliseconds < 1000
        ? $"{value.TotalMilliseconds:0.###} ms"
        : $"{value.TotalSeconds:0.###} s";

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim();
    }

    private sealed record EnumOption<T>(T Value, string Label) where T : struct, Enum
    {
        public override string ToString() => Label;
    }
}
