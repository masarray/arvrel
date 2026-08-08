using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private const double P03ConfigurationWidth = 300.0;
    private const double P04FaceplateHeight = 580.0;
    private const double P04DeviceCoreHeight = 548.0;

    private bool _p03HardwareApplied;
    private bool _p03TimerHooked;
    private bool _p03ConfigurationVisibilityHooked;
    private int _p03ApplyRetries;

    static AvrWorkspaceControl()
    {
        EventManager.RegisterClassHandler(
            typeof(AvrWorkspaceControl),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(P03ClassLoaded),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(AvrWorkspaceControl),
            FrameworkElement.UnloadedEvent,
            new RoutedEventHandler(P03ClassUnloaded),
            handledEventsToo: true);
    }

    private static void P03ClassLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not AvrWorkspaceControl control)
            return;

        control.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(control.ApplyP03HardwarePolish));
    }

    private static void P03ClassUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not AvrWorkspaceControl control || !control._p03TimerHooked)
            return;

        control._timer.Tick -= control.P03HardwareTick;
        control._p03TimerHooked = false;
    }

    private void ApplyP03HardwarePolish()
    {
        // P0 installs the Viewbox/HMI on Loaded. If class-level Loaded runs first,
        // retry at a lower dispatcher priority instead of applying to the legacy HMI.
        if (_faceplateFitHost?.Child is not Border faceplate || _p0Hmi is null)
        {
            if (_p03ApplyRetries++ < 3)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(ApplyP03HardwarePolish));
            }
            return;
        }

        if (!_p03ConfigurationVisibilityHooked)
        {
            _p03ConfigurationVisibilityHooked = true;
            ConfigurationExpanded.IsVisibleChanged += P03ConfigurationVisibilityChanged;
        }

        // Native engineering-software behavior: Config is docked in its own column,
        // expanded by default, and never overlays the virtual physical device.
        SetConfigurationExpanded(true);
        ApplyP03DockedConfigurationGeometry();
        ApplyP03CompactConfigurationTypography();
        ApplyP03MetalFaceplate(faceplate);
        ApplyP03BlackGlassStatusDisplay();
        _p0Hmi.ApplyP03Layout();

        if (!_p03TimerHooked)
        {
            _timer.Tick += P03HardwareTick;
            _p03TimerHooked = true;
        }

        _p03HardwareApplied = true;
        P03HardwareTick(this, EventArgs.Empty);
    }

    private void P03ConfigurationVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        => ApplyP03DockedConfigurationGeometry();

    private void ApplyP03DockedConfigurationGeometry()
    {
        if (VisualTreeHelper.GetParent(ConfigurationPanel) is not Grid root || root.ColumnDefinitions.Count < 5)
            return;

        var expanded = ConfigurationExpanded.Visibility == Visibility.Visible;
        Grid.SetColumn(ConfigurationPanel, 4);
        Grid.SetColumnSpan(ConfigurationPanel, 1);
        ConfigurationPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        ConfigurationPanel.VerticalAlignment = VerticalAlignment.Stretch;
        ConfigurationPanel.Width = double.NaN;
        Panel.SetZIndex(ConfigurationPanel, 0);

        ConfigurationColumn.MinWidth = 38;
        ConfigurationColumn.MaxWidth = 320;
        ConfigurationColumn.Width = new GridLength(expanded ? P03ConfigurationWidth : 38.0);

        // Keep the virtual AVR centered in the remaining center column. The Viewbox
        // scales the complete fixed-ratio device uniformly; it never stretches it.
        if (_faceplateFitHost is not null)
        {
            _faceplateFitHost.Margin = new Thickness(4, 2, 4, 2);
            _faceplateFitHost.Stretch = Stretch.Uniform;
            _faceplateFitHost.StretchDirection = StretchDirection.Both;
        }
    }

    private void ApplyP03CompactConfigurationTypography()
    {
        var inter = new FontFamily("Inter");

        foreach (var tab in ConfigurationTabs.Items.OfType<TabItem>())
        {
            tab.Padding = new Thickness(5, 4, 5, 4);
            tab.FontSize = 8.5;
            tab.FontFamily = inter;
        }

        foreach (var box in FindVisualChildren<TextBox>(ConfigurationPanel))
        {
            box.FontFamily = inter;
            box.Height = 27;
            box.FontSize = Math.Min(box.FontSize > 0 ? box.FontSize : 10, 9.5);
        }

        foreach (var text in FindVisualChildren<TextBlock>(ConfigurationPanel))
            text.FontFamily = inter;
    }

    private static void ApplyP03MetalFaceplate(Border faceplate)
    {
        // Add physical metal above and below the existing 548 px device core while
        // preserving LCD/keypad geometry exactly. The complete device is still scaled
        // uniformly by the Viewbox, so this behaves like a taller real enclosure rather
        // than a responsive dashboard.
        faceplate.Height = P04FaceplateHeight;
        faceplate.MinHeight = P04FaceplateHeight;
        faceplate.MaxHeight = P04FaceplateHeight;

        if (faceplate.Child is FrameworkElement deviceCore)
        {
            deviceCore.Height = P04DeviceCoreHeight;
            deviceCore.MinHeight = P04DeviceCoreHeight;
            deviceCore.MaxHeight = P04DeviceCoreHeight;
            deviceCore.VerticalAlignment = VerticalAlignment.Center;
        }

        faceplate.Background = CreateP04BrushedMetalBrush();
        faceplate.BorderBrush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(101, 100, 94), 0.00),
                new(Color.FromRgb(224, 220, 207), 0.22),
                new(Color.FromRgb(125, 124, 117), 0.50),
                new(Color.FromRgb(232, 228, 215), 0.78),
                new(Color.FromRgb(92, 94, 91), 1.00)
            },
            new Point(0, 0),
            new Point(1, 0));
        faceplate.BorderThickness = new Thickness(1.6);
        faceplate.Effect = new DropShadowEffect
        {
            BlurRadius = 14,
            ShadowDepth = 2,
            Opacity = 0.32,
            Color = Color.FromRgb(4, 10, 14)
        };
    }

    private static ImageBrush CreateP04BrushedMetalBrush()
    {
        // Procedural gray-gold aluminium. The texture is generated once from a tiny
        // deterministic bitmap: strong horizontal correlation gives the brushed grain,
        // while low-amplitude micro variation prevents a synthetic flat-gradient look.
        // No external texture asset, shader, animation, or runtime allocation loop is used.
        const int width = 192;
        const int height = 36;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        static int ClampByte(int value) => Math.Clamp(value, 0, 255);
        static int HashNoise(int x, int y)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + 0x5A17);
                n = (n ^ (n >> 13)) * 1274126177u;
                n ^= n >> 16;
                return (int)(n % 13) - 6;
            }
        }

        for (var y = 0; y < height; y++)
        {
            var rowNoise = HashNoise(3, y) * 2;
            if (y % 9 == 0) rowNoise -= 9;
            else if (y % 7 == 0) rowNoise += 6;
            else if (y % 3 == 0) rowNoise -= 3;

            var smoothed = 0.0;
            for (var x = 0; x < width; x++)
            {
                smoothed = smoothed * 0.82 + HashNoise(x, y) * 0.18;
                var longitudinal = (int)Math.Round(2.5 * Math.Sin(x * 0.075 + y * 0.41));
                var centerHighlight = (int)Math.Round(5.0 * Math.Sin(Math.PI * x / Math.Max(1, width - 1)));
                var delta = rowNoise + (int)Math.Round(smoothed) + longitudinal + centerHighlight;

                // Warm neutral aluminium: silver first, with a restrained gray-gold cast.
                var r = ClampByte(197 + delta);
                var g = ClampByte(193 + delta);
                var b = ClampByte(181 + delta);

                var index = y * stride + x * 4;
                pixels[index + 0] = (byte)b;
                pixels[index + 1] = (byte)g;
                pixels[index + 2] = (byte)r;
                pixels[index + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, width, height),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, width, height),
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };
        brush.Freeze();
        return brush;
    }

    private void ApplyP03BlackGlassStatusDisplay()
    {
        var statusBorder = FindAncestorBorder(PowerLed);
        if (statusBorder is null)
            return;

        statusBorder.Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(5, 10, 13), 0.00),
                new(Color.FromRgb(17, 27, 32), 0.48),
                new(Color.FromRgb(4, 8, 11), 1.00)
            },
            new Point(0, 0),
            new Point(0, 1));
        statusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(75, 91, 100));
        statusBorder.BorderThickness = new Thickness(1);
        statusBorder.CornerRadius = new CornerRadius(5);
        statusBorder.Effect = new DropShadowEffect
        {
            BlurRadius = 6,
            ShadowDepth = 1,
            Opacity = 0.35,
            Color = Colors.Black
        };

        foreach (var text in FindVisualChildren<TextBlock>(statusBorder))
        {
            text.Foreground = new SolidColorBrush(Color.FromRgb(202, 216, 222));
            text.FontFamily = new FontFamily("Inter");
        }

        DeviceClockText.Foreground = new SolidColorBrush(Color.FromRgb(151, 210, 222));
    }

    private static Border? FindAncestorBorder(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is Border border)
                return border;
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private void P03HardwareTick(object? sender, EventArgs e)
    {
        if (!_p03HardwareApplied)
            return;

        var inactive = new SolidColorBrush(Color.FromRgb(48, 61, 67));
        var green = new SolidColorBrush(Color.FromRgb(69, 211, 123));
        var amber = new SolidColorBrush(Color.FromRgb(242, 174, 62));
        var red = new SolidColorBrush(Color.FromRgb(232, 91, 80));

        PowerLed.Fill = green;
        RunLed.Fill = _snapshot.SourceEnergized ? green : inactive;

        var severeBlock = _snapshot.Blocked && IsP03SevereBlockReason(_snapshot.Reason);
        BlockLed.Fill = !_snapshot.Blocked ? inactive : severeBlock ? red : amber;
        LimitLed.Fill = _snapshot.TapPosition <= _settings.MinimumTap || _snapshot.TapPosition >= _settings.MaximumTap
            ? amber
            : inactive;

        BlockLed.Effect = _snapshot.Blocked
            ? new DropShadowEffect
            {
                Color = severeBlock ? Color.FromRgb(232, 91, 80) : Color.FromRgb(242, 174, 62),
                BlurRadius = 7,
                ShadowDepth = 0,
                Opacity = 0.65
            }
            : null;

        _p0Hmi?.ApplyP03State(_snapshot, severeBlock);
    }

    private static bool IsP03SevereBlockReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return false;

        return reason.Contains("voltage", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("current", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("U<", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("U>", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("I<", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("I>", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("under", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("over", StringComparison.OrdinalIgnoreCase);
    }
}
