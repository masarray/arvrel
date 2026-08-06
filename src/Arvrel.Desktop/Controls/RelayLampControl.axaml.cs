using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Arvrel.Desktop.Controls;

public enum RelayLampTone
{
    Green,
    Amber,
    Red,
    Blue
}

public sealed partial class RelayLampControl : UserControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<RelayLampControl, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> InvertProperty =
        AvaloniaProperty.Register<RelayLampControl, bool>(nameof(Invert));

    public static readonly StyledProperty<RelayLampTone> ToneProperty =
        AvaloniaProperty.Register<RelayLampControl, RelayLampTone>(nameof(Tone), RelayLampTone.Green);

    private Ellipse? _halo;
    private Ellipse? _lens;
    private Ellipse? _highlight;

    public RelayLampControl()
    {
        InitializeComponent();
        _halo = this.FindControl<Ellipse>("Halo");
        _lens = this.FindControl<Ellipse>("Lens");
        _highlight = this.FindControl<Ellipse>("Highlight");
        UpdateVisualState();
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool Invert
    {
        get => GetValue(InvertProperty);
        set => SetValue(InvertProperty, value);
    }

    public RelayLampTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty ||
            change.Property == InvertProperty ||
            change.Property == ToneProperty)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (_halo is null || _lens is null || _highlight is null)
            return;

        var active = Invert ? !IsActive : IsActive;
        if (!active)
        {
            _halo.Fill = Brushes.Transparent;
            _halo.Opacity = 0;
            _lens.Fill = new SolidColorBrush(Color.Parse("#52636D"));
            _lens.Stroke = new SolidColorBrush(Color.Parse("#A6B1B7"));
            _highlight.Opacity = 0.30;
            return;
        }

        var color = Tone switch
        {
            RelayLampTone.Amber => Color.Parse("#F2A923"),
            RelayLampTone.Red => Color.Parse("#EF3945"),
            RelayLampTone.Blue => Color.Parse("#36A0E1"),
            _ => Color.Parse("#2EEA63")
        };

        _halo.Fill = new SolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B));
        _halo.Opacity = 0.72;
        _lens.Fill = new SolidColorBrush(color);
        _lens.Stroke = new SolidColorBrush(Color.Parse("#F2FFFFFF"));
        _highlight.Opacity = 0.88;
    }
}
