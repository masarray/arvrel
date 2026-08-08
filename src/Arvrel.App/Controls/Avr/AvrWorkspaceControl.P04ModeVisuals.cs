using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private bool _p04ModeVisualsInstalled;
    private bool _p04TickAttached;

    private static readonly Brush P04InactiveButtonBrush = P04Freeze(Color.FromRgb(53, 67, 74));
    private static readonly Brush P04InactiveBorderBrush = P04Freeze(Color.FromRgb(117, 133, 141));
    private static readonly Brush P04InactiveTextBrush = P04Freeze(Color.FromRgb(220, 232, 237));

    private static readonly Brush P04RemoteButtonBrush = P04Freeze(Color.FromRgb(12, 92, 142));
    private static readonly Brush P04RemoteBorderBrush = P04Freeze(Color.FromRgb(105, 204, 255));
    private static readonly Brush P04RemoteTextBrush = P04Freeze(Color.FromRgb(242, 251, 255));

    private static readonly Brush P04AutoButtonBrush = P04Freeze(Color.FromRgb(18, 108, 68));
    private static readonly Brush P04AutoBorderBrush = P04Freeze(Color.FromRgb(104, 226, 160));
    private static readonly Brush P04AutoTextBrush = P04Freeze(Color.FromRgb(240, 255, 247));

    private static readonly Brush P04AttentionButtonBrush = P04Freeze(Color.FromRgb(132, 91, 21));
    private static readonly Brush P04AttentionBorderBrush = P04Freeze(Color.FromRgb(247, 194, 79));
    private static readonly Brush P04AttentionTextBrush = P04Freeze(Color.FromRgb(255, 249, 226));

    private static readonly Effect P04RemoteGlow = P04Glow(Color.FromRgb(84, 194, 247));
    private static readonly Effect P04AutoGlow = P04Glow(Color.FromRgb(79, 210, 134));
    private static readonly Effect P04AttentionGlow = P04Glow(Color.FromRgb(240, 177, 55));

    private void InstallP04ModeVisuals()
    {
        if (_p04ModeVisualsInstalled)
            return;

        _p04ModeVisualsInstalled = true;
        Loaded += P04ModeVisuals_Loaded;
        Unloaded += P04ModeVisuals_Unloaded;
        AddHandler(Button.ClickEvent, new RoutedEventHandler(P04AnyButton_Click), handledEventsToo: true);
    }

    private void P04ModeVisuals_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_p04TickAttached)
        {
            _timer.Tick += P04ModeVisualTick;
            _p04TickAttached = true;
        }

        InstallP05ButtonHoverVisuals();
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RefreshP04ModeVisuals));
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ApplyP06MetalSurface));
    }

    private void P04ModeVisuals_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_p04TickAttached)
            return;

        _timer.Tick -= P04ModeVisualTick;
        _p04TickAttached = false;
    }

    private void P04ModeVisualTick(object? sender, EventArgs e)
        => RefreshP04ModeVisuals();

    private void P04AnyButton_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                if (!IsLoaded)
                    return;

                P03HardwareTick(this, EventArgs.Empty);
                RefreshP04ModeVisuals();
            }));
    }

    private void RefreshP04ModeVisuals()
    {
        if (!IsInitialized)
            return;

        var snapshot = _snapshot;
        AutoModeButton.IsEnabled = true;
        ManualModeButton.IsEnabled = true;

        ApplyP04ModeButton(
            LocalAuthorityButton,
            snapshot.Authority == AvrControlAuthority.Local,
            P04AttentionButtonBrush,
            P04AttentionBorderBrush,
            P04AttentionTextBrush,
            P04AttentionGlow,
            "LOCAL_ACTIVE");

        ApplyP04ModeButton(
            RemoteAuthorityButton,
            snapshot.Authority == AvrControlAuthority.Remote,
            P04RemoteButtonBrush,
            P04RemoteBorderBrush,
            P04RemoteTextBrush,
            P04RemoteGlow,
            "REMOTE_ACTIVE");

        ApplyP04ModeButton(
            AutoModeButton,
            snapshot.Mode == AvrOperatingMode.Automatic,
            P04AutoButtonBrush,
            P04AutoBorderBrush,
            P04AutoTextBrush,
            P04AutoGlow,
            "AUTO_ACTIVE");

        ApplyP04ModeButton(
            ManualModeButton,
            snapshot.Mode == AvrOperatingMode.Manual,
            P04AttentionButtonBrush,
            P04AttentionBorderBrush,
            P04AttentionTextBrush,
            P04AttentionGlow,
            "MANUAL_ACTIVE");

        _p0Hmi?.ApplyP04ModeBadges(snapshot);
    }

    private static void ApplyP04ModeButton(
        Button button,
        bool active,
        Brush activeBackground,
        Brush activeBorder,
        Brush activeText,
        Effect activeGlow,
        string activeTag)
    {
        button.Tag = active ? activeTag : null;
        button.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
        button.Background = active ? activeBackground : P04InactiveButtonBrush;
        button.BorderBrush = active ? activeBorder : P04InactiveBorderBrush;

        // P05 intentionally uses a bright white hover face. The periodic P04 refresh
        // must not paint the legend white again while the pointer is still over it.
        button.Foreground = button.IsMouseOver
            ? P05FlashTextBrush
            : active ? activeText : P04InactiveTextBrush;

        button.Effect = active ? activeGlow : null;
    }

    private static Brush P04Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Effect P04Glow(Color color)
    {
        var effect = new DropShadowEffect
        {
            Color = color,
            BlurRadius = 11,
            ShadowDepth = 0,
            Opacity = 0.70
        };
        effect.Freeze();
        return effect;
    }
}
