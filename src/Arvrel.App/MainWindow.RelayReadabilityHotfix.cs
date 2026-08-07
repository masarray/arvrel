using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _relayReadabilityHotfixArmed;
    private bool _relayReadabilityHotfixInitialized;

    /// <summary>
    /// P0.1 relay-bay correction. P0 intentionally made the analysis pane dominant,
    /// but the 560 px native P6 faceplate then spent too much time downscaled. This
    /// coordinator restores a readable relay width while keeping the clean P0 shell.
    /// </summary>
    internal void InitializeRelayReadabilityHotfix()
    {
        if (_relayReadabilityHotfixInitialized || _relayReadabilityHotfixArmed)
            return;

        _relayReadabilityHotfixArmed = true;

        // Catch the first layout pass after P0 changes the workspace so the relay
        // does not remain visibly underscaled after startup.
        LayoutUpdated += RelayReadabilityStartupLayoutUpdated;
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(TryFinalizeRelayReadabilityHotfix));
    }

    private void RelayReadabilityStartupLayoutUpdated(object? sender, EventArgs e)
    {
        if (_p0GlobalUxInitialized)
            TryFinalizeRelayReadabilityHotfix();
    }

    private void TryFinalizeRelayReadabilityHotfix()
    {
        if (_relayReadabilityHotfixInitialized)
            return;

        if (!IsLoaded || !_p0GlobalUxInitialized)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(TryFinalizeRelayReadabilityHotfix));
            return;
        }

        LayoutUpdated -= RelayReadabilityStartupLayoutUpdated;
        _relayReadabilityHotfixArmed = false;
        _relayReadabilityHotfixInitialized = true;

        // P0 subscribes its SizeChanged handler before this one. This handler therefore
        // runs afterwards and keeps the readability contract as the final geometry.
        SizeChanged += RelayReadabilityHotfix_SizeChanged;
        Closed += RelayReadabilityHotfix_Closed;

        ApplyRelayReadabilityLayout();
    }

    private void RelayReadabilityHotfix_SizeChanged(object sender, SizeChangedEventArgs e)
        => ApplyRelayReadabilityLayout();

    private void RelayReadabilityHotfix_Closed(object? sender, EventArgs e)
    {
        SizeChanged -= RelayReadabilityHotfix_SizeChanged;
        LayoutUpdated -= RelayReadabilityStartupLayoutUpdated;
        Closed -= RelayReadabilityHotfix_Closed;
    }

    private void ApplyRelayReadabilityLayout()
    {
        if (Content is not Grid root)
            return;

        var workspace = root.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => Grid.GetRow(grid) == 2 && grid.ColumnDefinitions.Count == 3);
        if (workspace is null)
            return;

        var compact = ActualWidth > 0 && ActualWidth < 1380;

        // Target geometry:
        // - 1600 px class displays: relay bay reaches roughly 580-590 px so the
        //   560 px P6 hardware canvas is effectively native-size rather than shrunk.
        // - 1366 px class displays: keep roughly 500-540 px for usable labels/LCD.
        // - 1280 px minimum: preserve analysis usability while avoiding the old ~420 px bay.
        workspace.ColumnDefinitions[0].Width = new GridLength(compact ? 1.55 : 1.65, GridUnitType.Star);
        workspace.ColumnDefinitions[0].MinWidth = compact ? 720 : 760;
        workspace.ColumnDefinitions[1].Width = new GridLength(8);
        workspace.ColumnDefinitions[2].Width = new GridLength(1.0, GridUnitType.Star);
        workspace.ColumnDefinitions[2].MinWidth = compact ? 490 : 540;
        workspace.ColumnDefinitions[2].MaxWidth = compact ? 545 : 590;

        var relayBay = workspace.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 2);
        if (relayBay is not null)
        {
            // P6 already owns an internal 8 px hardware margin. Keep the bay breathing
            // room minimal so available width goes to readable hardware, not dead space.
            relayBay.Padding = new Thickness(4);
        }
    }
}
