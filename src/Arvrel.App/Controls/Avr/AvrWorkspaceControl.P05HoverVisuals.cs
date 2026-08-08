using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private bool _p05HoverInstalled;

    private static readonly Brush P05HoverBrush = P05Freeze(Color.FromRgb(60, 76, 85));
    private static readonly Brush P05HoverBorderBrush = P05Freeze(Color.FromRgb(104, 176, 208));
    private static readonly Brush P05HoverTextBrush = P05Freeze(Color.FromRgb(244, 250, 252));

    private static readonly Brush P05RemoteHoverBrush = P05Freeze(Color.FromRgb(15, 116, 170));
    private static readonly Brush P05RemoteHoverBorderBrush = P05Freeze(Color.FromRgb(139, 218, 255));

    private static readonly Brush P05AutoHoverBrush = P05Freeze(Color.FromRgb(20, 127, 77));
    private static readonly Brush P05AutoHoverBorderBrush = P05Freeze(Color.FromRgb(135, 238, 180));

    private static readonly Brush P05AttentionHoverBrush = P05Freeze(Color.FromRgb(153, 101, 22));
    private static readonly Brush P05AttentionHoverBorderBrush = P05Freeze(Color.FromRgb(255, 208, 100));

    private void InstallP05ButtonHoverVisuals()
    {
        if (_p05HoverInstalled)
            return;

        _p05HoverInstalled = true;
        foreach (var button in FindVisualChildren<Button>(this))
        {
            if (!IsP05HardwareButton(button))
                continue;

            button.MouseEnter -= P05Button_MouseEnter;
            button.MouseLeave -= P05Button_MouseLeave;
            button.MouseEnter += P05Button_MouseEnter;
            button.MouseLeave += P05Button_MouseLeave;
        }
    }

    private void P05Button_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button button)
            return;

        // The legacy DeviceButton template evaluates its near-white IsMouseOver trigger
        // after MouseEnter. Re-apply our local chrome values at Render priority, after
        // template triggers have settled, so the white flash can never be presented.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                if (button.IsMouseOver)
                    ApplyP05Hover(button);
            }));
    }

    private void P05Button_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Button button)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                if (!button.IsMouseOver)
                    RestoreP05Chrome(button);
            }));
    }

    private static bool IsP05HardwareButton(Button? button)
    {
        if (button is null)
            return false;

        var tag = button.Tag?.ToString()?.ToUpperInvariant();
        if (tag is "ENTER" or "LEFT" or "RIGHT" or "BACK" or "HOME" or "MENU" or
            "REMOTE_ACTIVE" or "AUTO_ACTIVE" or "LOCAL_ACTIVE" or "MANUAL_ACTIVE")
            return true;

        return button.Name is "LocalAuthorityButton" or "RemoteAuthorityButton" or
            "AutoModeButton" or "ManualModeButton";
    }

    private static void ApplyP05Hover(Button button)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("Chrome", button) is not Border chrome)
            return;

        var tag = button.Tag?.ToString()?.ToUpperInvariant();
        (Brush background, Brush border) = tag switch
        {
            "REMOTE_ACTIVE" => (P05RemoteHoverBrush, P05RemoteHoverBorderBrush),
            "AUTO_ACTIVE" => (P05AutoHoverBrush, P05AutoHoverBorderBrush),
            "LOCAL_ACTIVE" or "MANUAL_ACTIVE" => (P05AttentionHoverBrush, P05AttentionHoverBorderBrush),
            _ => (P05HoverBrush, P05HoverBorderBrush)
        };

        chrome.Background = background;
        chrome.BorderBrush = border;

        // Lucide keypad glyphs are intentionally white. Keep text-based hardware keys
        // on the same high-contrast palette so hover never becomes white-on-white or
        // dark-on-dark, regardless of the legacy template's Foreground trigger.
        button.Foreground = P05HoverTextBrush;
    }

    private void RestoreP05Chrome(Button button)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("Chrome", button) is Border chrome)
        {
            chrome.ClearValue(Border.BackgroundProperty);
            chrome.ClearValue(Border.BorderBrushProperty);
        }

        // Re-assert semantic illumination on the left-side authority/mode keys. The
        // right-side Lucide keys legitimately keep the same near-white foreground.
        RefreshP04ModeVisuals();
    }

    private static Brush P05Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
