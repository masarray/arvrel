using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Arvrel.Protection;

namespace Arvrel.App.Services;

public sealed class VirtualInjectionRow : INotifyPropertyChanged, IDataErrorInfo
{
    private bool _isEnabled;
    private string _valueText = "0";
    private string _angleText = "0";

    public VirtualInjectionRow(VirtualInjectionSignal signal)
    {
        Signal = signal;
    }

    public VirtualInjectionSignal Signal { get; }

    public string SignalLabel => Signal switch
    {
        VirtualInjectionSignal.PhaseAVoltage => "V L1-E",
        VirtualInjectionSignal.PhaseBVoltage => "V L2-E",
        VirtualInjectionSignal.PhaseCVoltage => "V L3-E",
        VirtualInjectionSignal.NeutralVoltage => "V N",
        VirtualInjectionSignal.PhaseACurrent => "I L1",
        VirtualInjectionSignal.PhaseBCurrent => "I L2",
        VirtualInjectionSignal.PhaseCCurrent => "I L3",
        VirtualInjectionSignal.NeutralCurrent => "I N",
        _ => Signal.ToString()
    };

    public string Unit => Signal <= VirtualInjectionSignal.NeutralVoltage ? "V" : "A";

    public bool IsNeutral => Signal is VirtualInjectionSignal.NeutralVoltage or VirtualInjectionSignal.NeutralCurrent;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (Set(ref _isEnabled, value))
                OnPropertyChanged(nameof(Provenance));
        }
    }

    public string ValueText
    {
        get => _valueText;
        set => Set(ref _valueText, value);
    }

    public string AngleText
    {
        get => _angleText;
        set => Set(ref _angleText, value);
    }

    public string Provenance => IsNeutral
        ? IsEnabled ? "Explicit" : "Σ phase"
        : IsEnabled ? "Explicit" : "Off";

    public string Error
    {
        get
        {
            var valueError = ValidateValue();
            return string.IsNullOrEmpty(valueError) ? ValidateAngle() : valueError;
        }
    }

    public string this[string columnName] => columnName switch
    {
        nameof(ValueText) => ValidateValue(),
        nameof(AngleText) => ValidateAngle(),
        _ => string.Empty
    };

    public void Apply(VirtualInjectionChannel channel)
    {
        IsEnabled = channel.Enabled;
        ValueText = channel.Rms.ToString("0.###", CultureInfo.InvariantCulture);
        AngleText = VirtualInjectionChannel.NormalizeAngle(channel.AngleDegrees)
            .ToString("0.###", CultureInfo.InvariantCulture);
    }

    public bool TryCreateChannel(out VirtualInjectionChannel channel, out string error)
    {
        if (!TryParseEngineeringDouble(ValueText, out var value) || !double.IsFinite(value) || value < 0 || value > 1_000_000_000)
        {
            channel = new VirtualInjectionChannel(false, 0, 0);
            error = $"{SignalLabel}: RMS value must be a finite number from 0 to 1e9 {Unit}.";
            return false;
        }

        if (!TryParseEngineeringDouble(AngleText, out var angle) || !double.IsFinite(angle))
        {
            channel = new VirtualInjectionChannel(false, 0, 0);
            error = $"{SignalLabel}: angle must be a finite number.";
            return false;
        }

        channel = new VirtualInjectionChannel(IsEnabled, value, VirtualInjectionChannel.NormalizeAngle(angle));
        error = string.Empty;
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static bool TryParseEngineeringDouble(string? text, out double value)
    {
        var styles = NumberStyles.Float | NumberStyles.AllowThousands;
        return double.TryParse(text, styles, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, styles, CultureInfo.InvariantCulture, out value);
    }

    private string ValidateValue()
        => TryParseEngineeringDouble(ValueText, out var value) && double.IsFinite(value) && value is >= 0 and <= 1_000_000_000
            ? string.Empty
            : $"Enter 0 to 1e9 {Unit}.";

    private string ValidateAngle()
        => TryParseEngineeringDouble(AngleText, out var angle) && double.IsFinite(angle)
            ? string.Empty
            : "Enter a finite angle in degrees.";

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
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
