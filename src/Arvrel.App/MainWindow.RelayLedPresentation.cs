using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Arvrel.App;

public partial class MainWindow
{
    private static readonly Brush RelayToolbarLensOpacityMask = CreateToolbarLensOpacityMask();
    private bool _relayLedPresentationInitialized;

    internal void InitializeRelayLedPresentation()
    {
        if (_relayLedPresentationInitialized)
            return;

        _relayLedPresentationInitialized = true;

        // Faceplate lamps are owned exclusively by RelayIndicatorLampBehavior,
        // which builds a composite bezel/cavity/halo/lens assembly. This class
        // now configures only the compact toolbar lamp and never subscribes to
        // Fill changes or rewrites faceplate stroke/effect state.
        ConfigureToolbarHealthLed(TopHealthLed);
    }

    private static void ConfigureToolbarHealthLed(Ellipse led)
    {
        ArgumentNullException.ThrowIfNull(led);

        led.Width = 8;
        led.Height = 8;
        led.MinWidth = 8;
        led.MinHeight = 8;
        led.Stretch = Stretch.Fill;
        led.StrokeThickness = 1;
        led.OpacityMask = RelayToolbarLensOpacityMask;
        led.SnapsToDevicePixels = false;
        led.UseLayoutRounding = true;
        RenderOptions.SetEdgeMode(led, EdgeMode.Unspecified);
    }

    private static Brush CreateToolbarLensOpacityMask()
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
