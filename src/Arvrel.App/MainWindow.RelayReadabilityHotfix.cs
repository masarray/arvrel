using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private const double RelayNativeViewboxWidth = 576.0;
    private const double RelayNativeViewboxHeight = 726.0;
    private const double RelayBayDividerWidth = 8.0;

    private bool _relayReadabilityHotfixArmed;
    private bool _relayReadabilityHotfixInitialized;
    private bool _relayReadabilityLayoutQueued;
    private Grid? _relayReadabilityWorkspace;

    /// <summary>
    /// P0.2 relay-bay correction. The relay is hardware, not a sidebar: its width
    /// follows the usable workspace height so the fixed P6 faceplate keeps a stable
    /// physical/readability scale from restored windows through maximized displays.
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

        _relayReadabilityWorkspace = ResolveRelayReadabilityWorkspace();
        if (_relayReadabilityWorkspace is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(TryFinalizeRelayReadabilityHotfix));
            return;
        }

        LayoutUpdated -= RelayReadabilityStartupLayoutUpdated;
        _relayReadabilityHotfixArmed = false;
        _relayReadabilityHotfixInitialized = true;

        // P0 subscribes its SizeChanged handler before this one. We also subscribe
        // to the workspace itself because child ActualHeight is final only after the
        // maximize/restore measure pass has completed.
        SizeChanged += RelayReadabilityHotfix_SizeChanged;
        _relayReadabilityWorkspace.SizeChanged += RelayReadabilityWorkspace_SizeChanged;
        Closed += RelayReadabilityHotfix_Closed;

        ApplyRelayReadabilityLayout();
        QueueRelayReadabilityLayout();
    }

    private void RelayReadabilityHotfix_SizeChanged(object sender, SizeChangedEventArgs e)
        => QueueRelayReadabilityLayout();

    private void RelayReadabilityWorkspace_SizeChanged(object sender, SizeChangedEventArgs e)
        => QueueRelayReadabilityLayout();

    private void QueueRelayReadabilityLayout()
    {
        if (_relayReadabilityLayoutQueued)
            return;

        _relayReadabilityLayoutQueued = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                _relayReadabilityLayoutQueued = false;
                ApplyRelayReadabilityLayout();
            }));
    }

    private void RelayReadabilityHotfix_Closed(object? sender, EventArgs e)
    {
        SizeChanged -= RelayReadabilityHotfix_SizeChanged;
        LayoutUpdated -= RelayReadabilityStartupLayoutUpdated;

        if (_relayReadabilityWorkspace is not null)
            _relayReadabilityWorkspace.SizeChanged -= RelayReadabilityWorkspace_SizeChanged;

        Closed -= RelayReadabilityHotfix_Closed;
    }

    private Grid? ResolveRelayReadabilityWorkspace()
    {
        if (Content is not Grid root)
            return null;

        return root.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => Grid.GetRow(grid) == 2 && grid.ColumnDefinitions.Count == 3);
    }

    private void ApplyRelayReadabilityLayout()
    {
        var workspace = _relayReadabilityWorkspace ?? ResolveRelayReadabilityWorkspace();
        if (workspace is null)
            return;

        _relayReadabilityWorkspace = workspace;

        var compact = ActualWidth > 0 && ActualWidth < 1380;
        var analysisMinimumWidth = compact ? 700.0 : 760.0;
        var minimumRelayWidth = compact ? 488.0 : 520.0;

        // The P6 scaler contains a 560x710 hardware grid plus an 8 px margin on all
        // sides, so 576:726 is the actual visual aspect ratio the bay must preserve.
        // Width therefore grows with available height instead of being capped at a
        // desktop-only 590 px. This is what keeps a maximized relay filling the bay.
        var workspaceWidth = workspace.ActualWidth > 1
            ? workspace.ActualWidth
            : Math.Max(0, ActualWidth - 20);
        var workspaceHeight = workspace.ActualHeight > 1
            ? workspace.ActualHeight
            : Math.Max(0, ActualHeight - 148);

        var relayAspect = RelayNativeViewboxWidth / RelayNativeViewboxHeight;
        var desiredRelayWidth = Math.Max(
            minimumRelayWidth,
            Math.Round(Math.Max(0, workspaceHeight - 4) * relayAspect + 4));

        // Never let the relay consume the analysis instrument. At the 1280 px minimum
        // window this still leaves a usable waveform/phasor region; on 16:9 maximized
        // displays the height-derived width naturally lands around 38-42% of workspace.
        var maximumRelayWidth = Math.Max(
            minimumRelayWidth,
            workspaceWidth - analysisMinimumWidth - RelayBayDividerWidth);
        var relayWidth = Math.Min(desiredRelayWidth, maximumRelayWidth);

        workspace.ColumnDefinitions[0].Width = new GridLength(1.0, GridUnitType.Star);
        workspace.ColumnDefinitions[0].MinWidth = analysisMinimumWidth;
        workspace.ColumnDefinitions[1].Width = new GridLength(RelayBayDividerWidth);
        workspace.ColumnDefinitions[2].Width = new GridLength(relayWidth, GridUnitType.Pixel);
        workspace.ColumnDefinitions[2].MinWidth = 0;
        workspace.ColumnDefinitions[2].MaxWidth = double.PositiveInfinity;

        var relayBay = workspace.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 2);
        if (relayBay is not null)
        {
            // P6 owns its own hardware breathing room. The host should spend almost
            // all available geometry on the faceplate and remain stretchable in both axes.
            relayBay.Padding = new Thickness(2);
            relayBay.HorizontalAlignment = HorizontalAlignment.Stretch;
            relayBay.VerticalAlignment = VerticalAlignment.Stretch;
        }
    }
}
