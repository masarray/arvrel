using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private const int MaximumRelayFullFaceGlossAttempts = 5;

    private static readonly Brush RelayBodyDepthBackground = CreateVerticalGradient(
        ("#DCE6EB", 0.00),
        ("#C2CED5", 0.13),
        ("#AAB9C2", 0.45),
        ("#93A5B0", 0.76),
        ("#81939F", 1.00));

    private static readonly Brush RelayBodyDepthBorder = CreateDiagonalGradient(
        ("#F8FBFC", 0.00),
        ("#D3DDE2", 0.18),
        ("#899AA5", 0.56),
        ("#52636E", 0.82),
        ("#34434C", 1.00));

    private static readonly Brush RelayBodyFullFaceGloss = CreateDiagonalGradient(
        ("#24FFFFFF", 0.00),
        ("#14FFFFFF", 0.18),
        ("#09FFFFFF", 0.42),
        ("#03FFFFFF", 0.64),
        ("#00121B21", 0.78),
        ("#18121B21", 1.00));

    private bool _relayFullFaceGlossApplied;
    private int _relayFullFaceGlossAttempts;

    internal void InitializeRelayFullFaceGloss()
    {
        if (_relayFullFaceGlossApplied ||
            _relayFullFaceGlossAttempts >= MaximumRelayFullFaceGlossAttempts)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(ApplyRelayFullFaceGloss));
    }

    private void ApplyRelayFullFaceGloss()
    {
        if (_relayFullFaceGlossApplied ||
            _relayFullFaceGlossAttempts >= MaximumRelayFullFaceGlossAttempts)
            return;

        _relayFullFaceGlossAttempts++;
        var relayBody = VisualAncestors<Border>(HealthyLed)
            .FirstOrDefault(border => border.CornerRadius.TopLeft >= 8);
        if (relayBody?.Child is not Grid bodyGrid ||
            !string.Equals(bodyGrid.Tag?.ToString(), BodyBevelTag, StringComparison.Ordinal))
        {
            QueueRelayFullFaceGlossRetry();
            return;
        }

        // Reuse the one sheen created by the hardware shell. A second overlay was
        // the direct source of the large diagonal wedge seen in manual QA.
        var gloss = bodyGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => ReferenceEquals(border.Background, RelayBodyTopSheen));
        if (gloss is null)
        {
            QueueRelayFullFaceGlossRetry();
            return;
        }

        _relayFullFaceGlossApplied = true;

        // Molded depth belongs to the body itself, not to a bright reflection.
        relayBody.Background = RelayBodyDepthBackground;
        relayBody.BorderBrush = RelayBodyDepthBorder;
        relayBody.BorderThickness = new Thickness(1.8);

        Grid.SetRow(gloss, 0);
        Grid.SetColumn(gloss, 0);
        Grid.SetRowSpan(gloss, Math.Max(1, bodyGrid.RowDefinitions.Count));
        Grid.SetColumnSpan(gloss, Math.Max(1, bodyGrid.ColumnDefinitions.Count));

        gloss.Height = double.NaN;
        gloss.Width = double.NaN;
        gloss.Margin = new Thickness(1.6);
        gloss.CornerRadius = new CornerRadius(9.2);
        gloss.HorizontalAlignment = HorizontalAlignment.Stretch;
        gloss.VerticalAlignment = VerticalAlignment.Stretch;
        gloss.Background = RelayBodyFullFaceGloss;
        gloss.Opacity = 1.0;
        gloss.IsHitTestVisible = false;
        gloss.CacheMode = new BitmapCache(1.0);

        // The body background belongs to the parent Border. A negative child
        // Z-index therefore remains visible above that background while staying
        // behind every LCD, label, LED, button, and bezel in the body Grid.
        Panel.SetZIndex(gloss, -10);
    }

    private void QueueRelayFullFaceGlossRetry()
    {
        if (_relayFullFaceGlossAttempts >= MaximumRelayFullFaceGlossAttempts)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new Action(ApplyRelayFullFaceGloss));
    }
}

internal static class RelayFullFaceGlossBootstrap
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
            window.InitializeRelayFullFaceGloss();
    }
}
