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
        ("#30FFFFFF", 0.00),
        ("#1EFFFFFF", 0.20),
        ("#12FFFFFF", 0.45),
        ("#08FFFFFF", 0.72),
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

        // A child without an explicit Grid span occupies only row 0 / column 0.
        // That produced the narrow white vertical patch visible on the left side
        // of the relay. Explicitly cover every body-grid cell and keep the
        // reflection subtle enough to read as molded plastic, not a white film.
        Grid.SetRow(gloss, 0);
        Grid.SetColumn(gloss, 0);
        Grid.SetRowSpan(gloss, Math.Max(1, bodyGrid.RowDefinitions.Count));
        Grid.SetColumnSpan(gloss, Math.Max(1, bodyGrid.ColumnDefinitions.Count));

        gloss.Height = double.NaN;
        gloss.Width = double.NaN;
        gloss.Margin = new Thickness(2.4);
        gloss.CornerRadius = new CornerRadius(8.5);
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
