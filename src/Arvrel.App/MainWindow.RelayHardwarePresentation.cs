using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private static readonly Brush RelayPanelBackground = CreateHardwareGradient(
        "#E7ECF0", "#CCD5DC", "#B8C4CC");
    private static readonly Brush RelayBodyBackground = CreateHardwareGradient(
        "#C4D0D8", "#AAB9C4", "#91A3AF");
    private static readonly Brush RelayIndicatorBackground = CreateHardwareGradient(
        "#F4F7F9", "#E3E9ED", "#CDD7DE");
    private static readonly Brush RelayBodyBorder = CreateHardwareBrush("#71838F");
    private static readonly Brush RelayPanelBorder = CreateHardwareBrush("#A7B4BC");
    private static readonly Brush RelayInsetBorder = CreateHardwareBrush("#748791");

    private static readonly Effect RelayBodyShadow = CreateHardwareShadow(
        "#26343D", 15, 315, 6, 0.34);
    private static readonly Effect RelayInsetShadow = CreateHardwareShadow(
        "#26343D", 6, 135, 2, 0.32);
    private static readonly Effect RelayKeyShadow = CreateHardwareShadow(
        "#11191E", 8, 270, 4, 0.72);
    private static readonly Effect RelayResetShadow = CreateHardwareShadow(
        "#11191E", 10, 270, 5, 0.70);

    private static readonly HashSet<string> RelayHardwareKeyTips = new(StringComparer.Ordinal)
    {
        "Up",
        "Down",
        "Enter",
        "Next",
        "Cancel",
        "Reset trip"
    };

    private bool _relayHardwarePresentationInitialized;

    internal void InitializeRelayHardwarePresentation()
    {
        if (_relayHardwarePresentationInitialized)
            return;

        // Other faceplate partials build/reparent the LCD at Loaded priority.
        // Apply the hardware shell only after those owners have settled.
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(ApplyRelayHardwarePresentation));
    }

    private void ApplyRelayHardwarePresentation()
    {
        if (_relayHardwarePresentationInitialized)
            return;

        var borderAncestors = VisualAncestors<Border>(HealthyLed).ToArray();
        var indicatorPanel = borderAncestors.FirstOrDefault();
        var relayBody = borderAncestors.FirstOrDefault(border => border.CornerRadius.TopLeft >= 8);
        var relayPanel = relayBody is null
            ? null
            : borderAncestors.SkipWhile(border => !ReferenceEquals(border, relayBody)).Skip(1).FirstOrDefault();

        if (indicatorPanel is null || relayBody is null || relayPanel is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(ApplyRelayHardwarePresentation));
            return;
        }

        _relayHardwarePresentationInitialized = true;

        relayPanel.Background = RelayPanelBackground;
        relayPanel.BorderBrush = RelayPanelBorder;
        relayPanel.BorderThickness = new Thickness(1);
        relayPanel.SnapsToDevicePixels = true;

        relayBody.Background = RelayBodyBackground;
        relayBody.BorderBrush = RelayBodyBorder;
        relayBody.BorderThickness = new Thickness(1.25);
        relayBody.CornerRadius = new CornerRadius(10);
        relayBody.Effect = RelayBodyShadow;
        relayBody.CacheMode = new BitmapCache(1.0);

        indicatorPanel.Background = RelayIndicatorBackground;
        indicatorPanel.BorderBrush = RelayInsetBorder;
        indicatorPanel.BorderThickness = new Thickness(1);
        indicatorPanel.Effect = RelayInsetShadow;

        ApplyRelayLcdRecess(relayBody);
        ApplyRelayKeyDepth(relayBody);
    }

    private void ApplyRelayLcdRecess(Border relayBody)
    {
        if (_relayLcdHeader is null)
            return;

        var lcdBezel = VisualDescendants<Border>(relayBody)
            .FirstOrDefault(border => IsVisualAncestor(border, _relayLcdHeader));
        if (lcdBezel is null)
            return;

        lcdBezel.BorderBrush = RelayInsetBorder;
        lcdBezel.BorderThickness = new Thickness(4.5);
        lcdBezel.CornerRadius = new CornerRadius(7);
        lcdBezel.Effect = RelayInsetShadow;
    }

    private static void ApplyRelayKeyDepth(Border relayBody)
    {
        foreach (var button in VisualDescendants<Button>(relayBody))
        {
            var toolTip = button.ToolTip?.ToString();
            if (toolTip is not null && RelayHardwareKeyTips.Contains(toolTip))
            {
                button.Effect = toolTip == "Reset trip" ? RelayResetShadow : RelayKeyShadow;
                button.CacheMode = new BitmapCache(1.0);
                continue;
            }

            if (!ContainsHardwareText(button.Content as DependencyObject, "RESET TRIP"))
                continue;

            button.Effect = RelayResetShadow;
            button.CacheMode = new BitmapCache(1.0);
        }
    }

    private static bool ContainsHardwareText(DependencyObject? node, string expected)
    {
        if (node is null)
            return false;
        if (node is TextBlock text && string.Equals(text.Text, expected, StringComparison.Ordinal))
            return true;

        foreach (var child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is DependencyObject dependency && ContainsHardwareText(dependency, expected))
                return true;
        }
        return false;
    }

    private static bool IsVisualAncestor(DependencyObject ancestor, DependencyObject descendant)
    {
        DependencyObject? current = descendant;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static IEnumerable<T> VisualAncestors<T>(DependencyObject source)
        where T : DependencyObject
    {
        DependencyObject? current = source;
        while ((current = VisualTreeHelper.GetParent(current)) is not null)
        {
            if (current is T typed)
                yield return typed;
        }
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;
            foreach (var descendant in VisualDescendants<T>(child))
                yield return descendant;
        }
    }

    private static Brush CreateHardwareGradient(string top, string middle, string bottom)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(top)!, 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(middle)!, 0.46));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(bottom)!, 1));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateHardwareBrush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }

    private static Effect CreateHardwareShadow(
        string value,
        double blurRadius,
        double direction,
        double depth,
        double opacity)
    {
        var effect = new DropShadowEffect
        {
            Color = (Color)ColorConverter.ConvertFromString(value)!,
            BlurRadius = blurRadius,
            Direction = direction,
            ShadowDepth = depth,
            Opacity = opacity,
            RenderingBias = RenderingBias.Performance
        };
        effect.Freeze();
        return effect;
    }
}

internal static class RelayHardwarePresentationBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.InitializeRelayHardwarePresentation();
    }
}
