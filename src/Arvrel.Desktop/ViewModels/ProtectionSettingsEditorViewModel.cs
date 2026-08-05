using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Arvrel.Protection;

namespace Arvrel.Desktop.ViewModels;

public sealed class ProtectionSettingsEditorViewModel : INotifyPropertyChanged
{
    private string _groupName = "GROUP A";
    private string _revisionText = "1";

    public ProtectionSettingsEditorViewModel()
    {
        Phase50 = new InstantaneousStageEditorViewModel("50P", "Phase instantaneous");
        Phase51 = new TimeStageEditorViewModel("51P", "Phase inverse time");
        Earth50 = new InstantaneousStageEditorViewModel("50N", "Earth instantaneous");
        Earth51 = new TimeStageEditorViewModel("51N", "Earth inverse time");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public InstantaneousStageEditorViewModel Phase50 { get; }
    public TimeStageEditorViewModel Phase51 { get; }
    public InstantaneousStageEditorViewModel Earth50 { get; }
    public TimeStageEditorViewModel Earth51 { get; }

    public IReadOnlyList<IecCurveFamily> CurveFamilies { get; } = Enum.GetValues<IecCurveFamily>();
    public IReadOnlyList<ProtectionResetMode> ResetModes { get; } = Enum.GetValues<ProtectionResetMode>();

    public string GroupName
    {
        get => _groupName;
        set => SetField(ref _groupName, value ?? string.Empty);
    }

    public string RevisionText
    {
        get => _revisionText;
        set => SetField(ref _revisionText, value ?? string.Empty);
    }

    public void Apply(ProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _groupName = settings.GroupName;
        _revisionText = settings.Revision.ToString(CultureInfo.InvariantCulture);
        Phase50.Apply(
            settings.PhaseInstantaneousEnabled,
            settings.PhaseInstantaneousPickupA,
            settings.PhaseInstantaneousDelay,
            settings.PhaseInstantaneousDropoutRatio);
        Phase51.Apply(
            settings.PhaseTimeEnabled,
            settings.PhaseTimePickupA,
            settings.PhaseTimeCurve,
            settings.PhaseTimeMultiplier,
            settings.PhaseTimeDefiniteDelay,
            settings.PhaseTimeMinimumOperateTime,
            settings.PhaseTimeDropoutRatio,
            settings.PhaseTimeResetMode,
            settings.PhaseTimeResetDelay);
        Earth50.Apply(
            settings.EarthInstantaneousEnabled,
            settings.EarthInstantaneousPickupA,
            settings.EarthInstantaneousDelay,
            settings.EarthInstantaneousDropoutRatio);
        Earth51.Apply(
            settings.EarthTimeEnabled,
            settings.EarthTimePickupA,
            settings.EarthTimeCurve,
            settings.EarthTimeMultiplier,
            settings.EarthTimeDefiniteDelay,
            settings.EarthTimeMinimumOperateTime,
            settings.EarthTimeDropoutRatio,
            settings.EarthTimeResetMode,
            settings.EarthTimeResetDelay);
        OnPropertyChanged(string.Empty);
    }

    public bool TryBuild(
        ProtectionSettings current,
        out ProtectionSettings settings,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(current);
        settings = current;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(GroupName))
        {
            error = "Setting group name is required.";
            return false;
        }

        if (!int.TryParse(RevisionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision) || revision < 1)
        {
            error = "Revision must be an integer greater than or equal to one.";
            return false;
        }

        if (!Phase50.TryRead(out var phase50, out error) ||
            !Phase51.TryRead(out var phase51, out error) ||
            !Earth50.TryRead(out var earth50, out error) ||
            !Earth51.TryRead(out var earth51, out error))
            return false;

        try
        {
            settings = current with
            {
                GroupName = GroupName.Trim(),
                Revision = revision,
                PhaseInstantaneousEnabled = phase50.Enabled,
                PhaseInstantaneousPickupA = phase50.Pickup,
                PhaseInstantaneousDelay = phase50.Delay,
                PhaseInstantaneousDropoutRatio = phase50.Dropout,
                PhaseTimeEnabled = phase51.Enabled,
                PhaseTimePickupA = phase51.Pickup,
                PhaseTimeCurve = phase51.Curve,
                PhaseTimeMultiplier = phase51.Multiplier,
                PhaseTimeDefiniteDelay = phase51.DefiniteDelay,
                PhaseTimeMinimumOperateTime = phase51.MinimumOperateTime,
                PhaseTimeDropoutRatio = phase51.Dropout,
                PhaseTimeResetMode = phase51.ResetMode,
                PhaseTimeResetDelay = phase51.ResetDelay,
                EarthInstantaneousEnabled = earth50.Enabled,
                EarthInstantaneousPickupA = earth50.Pickup,
                EarthInstantaneousDelay = earth50.Delay,
                EarthInstantaneousDropoutRatio = earth50.Dropout,
                EarthTimeEnabled = earth51.Enabled,
                EarthTimePickupA = earth51.Pickup,
                EarthTimeCurve = earth51.Curve,
                EarthTimeMultiplier = earth51.Multiplier,
                EarthTimeDefiniteDelay = earth51.DefiniteDelay,
                EarthTimeMinimumOperateTime = earth51.MinimumOperateTime,
                EarthTimeDropoutRatio = earth51.Dropout,
                EarthTimeResetMode = earth51.ResetMode,
                EarthTimeResetDelay = earth51.ResetDelay
            };
            settings.Validate();
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            settings = current;
            return false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class InstantaneousStageEditorViewModel : INotifyPropertyChanged
{
    private bool _enabled;
    private string _pickupText = "1";
    private string _delayMsText = "0";
    private string _dropoutText = "0.95";

    public InstantaneousStageEditorViewModel(string code, string label)
    {
        Code = code;
        Label = label;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Code { get; }
    public string Label { get; }

    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
    public string PickupText { get => _pickupText; set => SetField(ref _pickupText, value ?? string.Empty); }
    public string DelayMsText { get => _delayMsText; set => SetField(ref _delayMsText, value ?? string.Empty); }
    public string DropoutText { get => _dropoutText; set => SetField(ref _dropoutText, value ?? string.Empty); }

    public void Apply(bool enabled, double pickup, TimeSpan delay, double dropout)
    {
        _enabled = enabled;
        _pickupText = Format(pickup);
        _delayMsText = Format(delay.TotalMilliseconds);
        _dropoutText = Format(dropout);
        OnPropertyChanged(string.Empty);
    }

    public bool TryRead(out InstantaneousStageDraft draft, out string error)
    {
        draft = default;
        if (!TryPositive(PickupText, $"{Code} pickup", out var pickup, out error) ||
            !TryNonNegative(DelayMsText, $"{Code} delay", out var delayMs, out error) ||
            !TryRatio(DropoutText, $"{Code} dropout", out var dropout, out error))
            return false;

        draft = new InstantaneousStageDraft(Enabled, pickup, TimeSpan.FromMilliseconds(delayMs), dropout);
        return true;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    internal static bool TryPositive(string text, string name, out double value, out string error)
    {
        if (!TryNumber(text, out value) || value <= 0)
        {
            error = $"{name} must be a finite positive number.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    internal static bool TryNonNegative(string text, string name, out double value, out string error)
    {
        if (!TryNumber(text, out value) || value < 0)
        {
            error = $"{name} must be a finite non-negative number.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    internal static bool TryRatio(string text, string name, out double value, out string error)
    {
        if (!TryNumber(text, out value) || value is <= 0 or > 1)
        {
            error = $"{name} must be greater than zero and no greater than one.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    internal static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool TryNumber(string text, out double value)
        => InjectionChannelViewModel.TryParseEngineeringDouble(text, out value) && double.IsFinite(value);
}

public sealed class TimeStageEditorViewModel : INotifyPropertyChanged
{
    private bool _enabled;
    private string _pickupText = "1";
    private IecCurveFamily _curve;
    private string _multiplierText = "0.1";
    private string _definiteDelayMsText = "500";
    private string _minimumOperateMsText = "20";
    private string _dropoutText = "0.95";
    private ProtectionResetMode _resetMode;
    private string _resetDelayMsText = "1000";

    public TimeStageEditorViewModel(string code, string label)
    {
        Code = code;
        Label = label;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Code { get; }
    public string Label { get; }

    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
    public string PickupText { get => _pickupText; set => SetField(ref _pickupText, value ?? string.Empty); }
    public IecCurveFamily Curve { get => _curve; set => SetField(ref _curve, value); }
    public string MultiplierText { get => _multiplierText; set => SetField(ref _multiplierText, value ?? string.Empty); }
    public string DefiniteDelayMsText { get => _definiteDelayMsText; set => SetField(ref _definiteDelayMsText, value ?? string.Empty); }
    public string MinimumOperateMsText { get => _minimumOperateMsText; set => SetField(ref _minimumOperateMsText, value ?? string.Empty); }
    public string DropoutText { get => _dropoutText; set => SetField(ref _dropoutText, value ?? string.Empty); }
    public ProtectionResetMode ResetMode { get => _resetMode; set => SetField(ref _resetMode, value); }
    public string ResetDelayMsText { get => _resetDelayMsText; set => SetField(ref _resetDelayMsText, value ?? string.Empty); }

    public void Apply(
        bool enabled,
        double pickup,
        IecCurveFamily curve,
        double multiplier,
        TimeSpan definiteDelay,
        TimeSpan minimumOperateTime,
        double dropout,
        ProtectionResetMode resetMode,
        TimeSpan resetDelay)
    {
        _enabled = enabled;
        _pickupText = InstantaneousStageEditorViewModel.Format(pickup);
        _curve = curve;
        _multiplierText = InstantaneousStageEditorViewModel.Format(multiplier);
        _definiteDelayMsText = InstantaneousStageEditorViewModel.Format(definiteDelay.TotalMilliseconds);
        _minimumOperateMsText = InstantaneousStageEditorViewModel.Format(minimumOperateTime.TotalMilliseconds);
        _dropoutText = InstantaneousStageEditorViewModel.Format(dropout);
        _resetMode = resetMode;
        _resetDelayMsText = InstantaneousStageEditorViewModel.Format(resetDelay.TotalMilliseconds);
        OnPropertyChanged(string.Empty);
    }

    public bool TryRead(out TimeStageDraft draft, out string error)
    {
        draft = default;
        if (!InstantaneousStageEditorViewModel.TryPositive(PickupText, $"{Code} pickup", out var pickup, out error) ||
            !InstantaneousStageEditorViewModel.TryPositive(MultiplierText, $"{Code} TMS", out var multiplier, out error) ||
            !InstantaneousStageEditorViewModel.TryPositive(DefiniteDelayMsText, $"{Code} definite delay", out var definiteDelayMs, out error) ||
            !InstantaneousStageEditorViewModel.TryNonNegative(MinimumOperateMsText, $"{Code} minimum operate time", out var minimumOperateMs, out error) ||
            !InstantaneousStageEditorViewModel.TryRatio(DropoutText, $"{Code} dropout", out var dropout, out error) ||
            !InstantaneousStageEditorViewModel.TryPositive(ResetDelayMsText, $"{Code} reset delay", out var resetDelayMs, out error))
            return false;

        draft = new TimeStageDraft(
            Enabled,
            pickup,
            Curve,
            multiplier,
            TimeSpan.FromMilliseconds(definiteDelayMs),
            TimeSpan.FromMilliseconds(minimumOperateMs),
            dropout,
            ResetMode,
            TimeSpan.FromMilliseconds(resetDelayMs));
        return true;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public readonly record struct InstantaneousStageDraft(
    bool Enabled,
    double Pickup,
    TimeSpan Delay,
    double Dropout);

public readonly record struct TimeStageDraft(
    bool Enabled,
    double Pickup,
    IecCurveFamily Curve,
    double Multiplier,
    TimeSpan DefiniteDelay,
    TimeSpan MinimumOperateTime,
    double Dropout,
    ProtectionResetMode ResetMode,
    TimeSpan ResetDelay);
