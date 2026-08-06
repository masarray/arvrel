using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Arvrel.App.Controls.VirtualRelay;

/// <summary>
/// A physical relay annunciator lamp. State owners continue to write a simple
/// logical colour to <see cref="Lens"/>; this control supplies one shared metal,
/// cavity, radial-lens, reflection and emitted-light model for every state.
/// </summary>
public partial class RelayLampControl : UserControl
{
    private static readonly DependencyPropertyDescriptor? FillDescriptor =
        DependencyPropertyDescriptor.FromProperty(Shape.FillProperty, typeof(Ellipse));

    private bool _listening;

    public RelayLampControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_listening)
        {
            FillDescriptor?.AddValueChanged(Lens, OnLensFillChanged);
            _listening = true;
        }

        UpdateOptics();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_listening)
            return;

        FillDescriptor?.RemoveValueChanged(Lens, OnLensFillChanged);
        _listening = false;
    }

    private void OnLensFillChanged(object? sender, EventArgs e)
        => UpdateOptics();

    private void UpdateOptics()
    {
        if (Lens.Fill is not SolidColorBrush solid)
        {
            SetPassiveOptics();
            return;
        }

        var color = solid.Color;
        if (IsPassive(color))
        {
            SetPassiveOptics();
            return;
        }

        var emitted = ResolveEmittedColor(color);
        var haloBrush = new SolidColorBrush(Color.FromArgb(154, emitted.R, emitted.G, emitted.B));
        haloBrush.Freeze();

        var glow = new DropShadowEffect
        {
            Color = emitted,
            BlurRadius = 8.5,
            Direction = 0,
            ShadowDepth = 0,
            Opacity = 0.58,
            RenderingBias = RenderingBias.Quality
        };
        glow.Freeze();

        Halo.Fill = haloBrush;
        Halo.Effect = glow;
        Halo.Opacity = 0.58;

        Lens.Opacity = 0.90;
        LensOptic.Fill = CreateActiveLensBrush(emitted);
        LensOptic.Opacity = 1.0;
        LensRim.Opacity = 0.70;
        LowerReflection.Opacity = 0.42;
        Highlight.Opacity = 0.82;
    }

    private void SetPassiveOptics()
    {
        Halo.Fill = Brushes.Transparent;
        Halo.Effect = null;
        Halo.Opacity = 0;

        Lens.Opacity = 0.96;
        LensOptic.Fill = Brushes.Transparent;
        LensOptic.Opacity = 0;
        LensRim.Opacity = 0.30;
        LowerReflection.Opacity = 0.22;
        Highlight.Opacity = 0.28;
    }

    private static RadialGradientBrush CreateActiveLensBrush(Color emitted)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.50, 0.50),
            GradientOrigin = new Point(0.30, 0.25),
            RadiusX = 0.74,
            RadiusY = 0.74
        };

        brush.GradientStops.Add(new GradientStop(Mix(emitted, Colors.White, 0.78), 0.00));
        brush.GradientStops.Add(new GradientStop(Mix(emitted, Colors.White, 0.28), 0.24));
        brush.GradientStops.Add(new GradientStop(emitted, 0.58));
        brush.GradientStops.Add(new GradientStop(Mix(emitted, Colors.Black, 0.46), 1.00));
        brush.Freeze();
        return brush;
    }

    private static Color Mix(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);

        static byte MixChannel(byte start, byte end, double ratio)
            => (byte)Math.Round(start + ((end - start) * ratio));

        return Color.FromArgb(
            255,
            MixChannel(from.R, to.R, amount),
            MixChannel(from.G, to.G, amount),
            MixChannel(from.B, to.B, amount));
    }

    private static bool IsPassive(Color color)
    {
        const int offR = 0x52;
        const int offG = 0x63;
        const int offB = 0x6D;
        var distance = Math.Sqrt(
            Math.Pow(color.R - offR, 2) +
            Math.Pow(color.G - offG, 2) +
            Math.Pow(color.B - offB, 2));
        return distance < 46;
    }

    private static Color ResolveEmittedColor(Color color)
    {
        if (color.R > color.G * 1.25 && color.R > color.B * 1.25)
            return Color.FromRgb(245, 48, 55);
        if (color.G > color.R * 1.18 && color.G > color.B * 1.12)
            return Color.FromRgb(42, 234, 99);
        if (color.B > color.R * 1.12 && color.B > color.G * 1.05)
            return Color.FromRgb(52, 160, 225);
        return Color.FromRgb(246, 168, 35);
    }
}
