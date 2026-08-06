using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Arvrel.Desktop.Controls;

public sealed partial class RelayLamp : UserControl
{
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<RelayLamp, bool>(nameof(IsOn));

    public static readonly StyledProperty<Color> ActiveColorProperty =
        AvaloniaProperty.Register<RelayLamp, Color>(
            nameof(ActiveColor),
            Color.Parse("#45B768"));

    public RelayLamp()
    {
        InitializeComponent();
        UpdateOptics();
    }

    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public Color ActiveColor
    {
        get => GetValue(ActiveColorProperty);
        set => SetValue(ActiveColorProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOnProperty || change.Property == ActiveColorProperty)
            UpdateOptics();
    }

    private void UpdateOptics()
    {
        if (Lens is null || Halo is null)
            return;

        if (!IsOn)
        {
            Lens.Fill = new SolidColorBrush(Color.Parse("#52636D"));
            Halo.Fill = Brushes.Transparent;
            Halo.Opacity = 0;
            return;
        }

        Lens.Fill = new SolidColorBrush(ActiveColor);
        Halo.Fill = new SolidColorBrush(Color.FromArgb(120, ActiveColor.R, ActiveColor.G, ActiveColor.B));
        Halo.Opacity = 0.72;
    }
}
