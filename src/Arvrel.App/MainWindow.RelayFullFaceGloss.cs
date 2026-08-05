using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private const int MaximumRelayFullFaceGlossAttempts = 5;

    private static readonly Brush RelayBodyFullFaceGloss = CreateDiagonalGradient(
        ("#66FFFFFF", 0.00),
        ("#38FFFFFF", 0.18),
        ("#20FFFFFF", 0.42),
        ("#0CFFFFFF", 0.68),
        ("#00FFFFFF", 1.00));

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

        var gloss = bodyGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => ReferenceEquals(border.Background, RelayBodyTopSheen));
        if (gloss is null)
        {
            QueueRelayFullFaceGlossRetry();
            return;
        }

        _relayFullFaceGlossApplied = true;

        // The previous 62 px strip sat above the header content and read as a
        // bright patch. Stretch the material reflection across the complete
        // molded face and keep it behind labels, LCD, LEDs and controls.
        gloss.Height = double.NaN;
        gloss.Width = double.NaN;
        gloss.Margin = new Thickness(2.2);
        gloss.CornerRadius = new CornerRadius(9);
        gloss.HorizontalAlignment = HorizontalAlignment.Stretch;
        gloss.VerticalAlignment = VerticalAlignment.Stretch;
        gloss.Background = RelayBodyFullFaceGloss;
        gloss.Opacity = 1.0;
        gloss.IsHitTestVisible = false;
        Panel.SetZIndex(gloss, -10);
    }

    private void QueueRelayFullFaceGlossRetry()
    {
        if (_relayFullFaceGlossAttempts >= MaximumRelayFullFaceGlossAttempts)
            return;

        // SystemIdle runs below the hardware presentation's ApplicationIdle
        // work item, preventing a fast retry loop from exhausting before the
        // original body bevel and sheen have been attached.
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
