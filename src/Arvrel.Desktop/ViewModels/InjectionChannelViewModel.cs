using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Arvrel.Protection;

namespace Arvrel.Desktop.ViewModels;

public sealed class InjectionChannelViewModel : INotifyPropertyChanged
{
    private bool _enabled;
    private string _rmsText = "0";
    private string _angleText = "0";

    public InjectionChannelViewModel(VirtualInjectionSignal signal)
        => Signal = signal;

    public event PropertyChangedEventHandler? PropertyChanged;

    public VirtualInjectionSignal Signal { get; }

    public string SignalLabel => Signal switch
    {
        VirtualInjectionSignal.PhaseAVoltage => "VA",
        VirtualInjectionSignal.PhaseBVoltage => "VB",
        VirtualInjectionSignal.PhaseCVoltage => "VC",
        VirtualInjectionSignal.NeutralVoltage => "VN / 3V0",
        VirtualInjectionSignal.PhaseACurrent => "IA",
        VirtualInjectionSignal.PhaseBCurrent => "IB",
        VirtualInjectionSignal.PhaseCCurrent => "IC",
        VirtualInjectionSignal.NeutralCurrent => "IN / 3I0",
        _ => Signal.ToString()
    };

    public string Unit => Signal is
        VirtualInjectionSignal.PhaseAVoltage or
        VirtualInjectionSignal.PhaseBVoltage or
        VirtualInjectionSignal.PhaseCVoltage or
        VirtualInjectionSignal.NeutralVoltage
            ? "V"
            : "A";

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!SetField(ref _enabled, value))
                return;
            OnPropertyChanged(nameof(Provenance));
        }
    }

    public string RmsText
    {
        get => _rmsText;
        set => SetField(ref _rmsText, value ?? string.Empty);
    }

    public string AngleText
    {
        get => _angleText;
        set => SetField(ref _angleText, value ?? string.Empty);
    }

    public string Provenance => Signal switch
    {
        VirtualInjectionSignal.NeutralCurrent => Enabled ? "explicit IN" : "IA+IB+IC",
        VirtualInjectionSignal.NeutralVoltage => Enabled ? "explicit VN" : "VA+VB+VC",
        _ => "explicit phase"
    };

    public void Apply(VirtualInjectionChannel channel)
    {
        _enabled = channel.Enabled;
        _rmsText = channel.Rms.ToString("0.###", CultureInfo.InvariantCulture);
        _angleText = channel.AngleDegrees.ToString("0.###", CultureInfo.InvariantCulture);
        OnPropertyChanged(string.Empty);
    }

    public bool TryCreateChannel(out VirtualInjectionChannel channel, out string error)
    {
        channel = new VirtualInjectionChannel(false, 0, 0);
        error = string.Empty;

        if (!TryParseEngineeringDouble(RmsText, out var rms) || !double.IsFinite(rms) || rms < 0)
        {
            error = $"{SignalLabel} RMS must be a finite non-negative number.";
            return false;
        }

        if (!TryParseEngineeringDouble(AngleText, out var angle) || !double.IsFinite(angle))
        {
            error = $"{SignalLabel} angle must be a finite number.";
            return false;
        }

        try
        {
            channel = new VirtualInjectionChannel(Enabled, rms, angle).Normalize();
            channel.Validate(SignalLabel);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryParseEngineeringDouble(string? text, out double value)
    {
        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        return double.TryParse(text, styles, CultureInfo.InvariantCulture, out value) ||
               double.TryParse(text, styles, CultureInfo.CurrentCulture, out value);
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
