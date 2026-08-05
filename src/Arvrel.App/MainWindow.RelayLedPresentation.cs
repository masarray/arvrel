using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Arvrel.App;

public partial class MainWindow
{
    private static readonly DependencyPropertyDescriptor? RelayLedFillDescriptor =
        DependencyPropertyDescriptor.FromProperty(Shape.FillProperty, typeof(Shape));

    private static readonly Brush RelayLedLensOpacityMask = CreateLensOpacityMask();
    private static readonly Brush RelayLedOffStroke = CreateFrozenBrush("#768691");
    private static readonly Brush RelayLedHealthyStroke = CreateFrozenBrush("#9BD7A6");
    private static readonly Brush RelayLedWarningStroke = CreateFrozenBrush("#E8C37E");
    private static readonly Brush RelayLedTripStroke = CreateFrozenBrush("#E99A97");
    private static readonly Brush RelayLedPhaseStroke = CreateFrozenBrush("#94C4E4");

    private static readonly Effect RelayLedOffEffect = CreateGlow("#26343D", 3.5, 0.32);
    private static readonly Effect RelayLedHealthyEffect = CreateGlow("#46C766", 11.5, 0.86);
    private static readonly Effect RelayLedWarningEffect = CreateGlow("#E5A935", 11.5, 0.86);
    private static readonly Effect RelayLedTripEffect = CreateGlow("#EF514D", 11.5, 0.92);
    private static readonly Effect RelayLedPhaseEffect = CreateGlow("#4A9FD6", 11.5, 0.86);

    private readonly List<Ellipse> _relayLedIndicators = new();
    private bool _relayLedPresentationInitialized;

    internal void InitializeRelayLedPresentation()
    {
        if (_relayLedPresentationInitialized)
            return;

        _relayLedPresentationInitialized = true;

        ConfigureRelayLed(HealthyLed);
        ConfigureRelayLed(PickupLed);
        ConfigureRelayLed(TripLed);
        ConfigureRelayLed(PhaseALed);
        ConfigureRelayLed(PhaseBLed);
        ConfigureRelayLed(PhaseCLed);
        ConfigureRelayLed(EarthLed);
        ConfigureRelayLed(BlockLed);

        // Keep the top application-health indicator compact while giving it the
        // same circular lens, edge, and state-matched glow behavior.
        ConfigureRelayLed(TopHealthLed, compact: true);

        Closed += (_, _) => ReleaseRelayLedPresentation();
    }

    private void ConfigureRelayLed(Ellipse led, bool compact = false)
    {
        ArgumentNullException.ThrowIfNull(led);

        var diameter = compact ? 8d : 12d;
        led.Width = diameter;
        led.Height = diameter;
        led.MinWidth = diameter;
        led.MinHeight = diameter;
        led.StrokeThickness = compact ? 1d : 1.25d;
        led.OpacityMask = RelayLedLensOpacityMask;
        led.SnapsToDevicePixels = false;
        RenderOptions.SetEdgeMode(led, EdgeMode.Unspecified);

        RelayLedFillDescriptor?.AddValueChanged(led, RelayLedFillChanged);
        _relayLedIndicators.Add(led);
        ApplyRelayLedPresentation(led);
    }

    private void ReleaseRelayLedPresentation()
    {
        foreach (var led in _relayLedIndicators)
            RelayLedFillDescriptor?.RemoveValueChanged(led, RelayLedFillChanged);
        _relayLedIndicators.Clear();
    }

    private static void RelayLedFillChanged(object? sender, EventArgs e)
    {
        if (sender is Ellipse led)
            ApplyRelayLedPresentation(led);
    }

    private static void ApplyRelayLedPresentation(Ellipse led)
    {
        var color = (led.Fill as SolidColorBrush)?.Color;
        var presentation = ResolveRelayLedPresentation(color);

        if (!ReferenceEquals(led.Stroke, presentation.Stroke))
            led.Stroke = presentation.Stroke;
        if (!ReferenceEquals(led.Effect, presentation.Effect))
            led.Effect = presentation.Effect;
        if (!AreClose(led.Opacity, presentation.Opacity))
            led.Opacity = presentation.Opacity;
    }

    private static RelayLedPresentation ResolveRelayLedPresentation(Color? color)
    {
        if (color is null)
            return new RelayLedPresentation(RelayLedOffStroke, RelayLedOffEffect, 0.92);

        return color.Value switch
        {
            { R: 70, G: 154, B: 88 } =>
                new RelayLedPresentation(RelayLedHealthyStroke, RelayLedHealthyEffect, 1.0),
            { R: 196, G: 139, B: 43 } =>
                new RelayLedPresentation(RelayLedWarningStroke, RelayLedWarningEffect, 1.0),
            { R: 199, G: 71, B: 67 } =>
                new RelayLedPresentation(RelayLedTripStroke, RelayLedTripEffect, 1.0),
            { R: 65, G: 139, B: 191 } =>
                new RelayLedPresentation(RelayLedPhaseStroke, RelayLedPhaseEffect, 1.0),
            _ => new RelayLedPresentation(RelayLedOffStroke, RelayLedOffEffect, 0.92)
        };
    }

    private static Brush CreateLensOpacityMask()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.52),
            GradientOrigin = new Point(0.31, 0.25),
            RadiusX = 0.72,
            RadiusY = 0.72,
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(248, 255, 255, 255), 0.38));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(218, 255, 255, 255), 0.78));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(186, 255, 255, 255), 1.0));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateFrozenBrush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }

    private static Effect CreateGlow(string value, double blurRadius, double opacity)
    {
        var color = (Color)ColorConverter.ConvertFromString(value)!;
        var effect = new DropShadowEffect
        {
            Color = color,
            BlurRadius = blurRadius,
            ShadowDepth = 0,
            Direction = 0,
            Opacity = opacity,
            RenderingBias = RenderingBias.Performance
        };
        effect.Freeze();
        return effect;
    }

    private static bool AreClose(double left, double right)
        => Math.Abs(left - right) < 0.0001;

    private readonly record struct RelayLedPresentation(
        Brush Stroke,
        Effect Effect,
        double Opacity);
}

internal static class RelayLedPresentationBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoaded));
    }

    private static void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.InitializeRelayLedPresentation();
    }
}
