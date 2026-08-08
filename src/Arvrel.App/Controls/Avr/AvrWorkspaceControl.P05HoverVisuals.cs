using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private bool _p05HoverInstalled;

    private static readonly Brush P05HoverBrush = P05Freeze(Color.FromRgb(66, 84, 94));
    private static readonly Brush P05HoverBorderBrush = P05Freeze(Color.FromRgb(111, 175, 203));

    private static readonly Brush P05RemoteHoverBrush = P05Freeze(Color.FromRgb(15, 118, 171));
    private static readonly Brush P05RemoteHoverBorderBrush = P05Freeze(Color.FromRgb(142, 219, 255));

    private static readonly Brush P05AutoHoverBrush = P05Freeze(Color.FromRgb(20, 129, 78));
    private static readonly Brush P05AutoHoverBorderBrush = P05Freeze(Color.FromRgb(139, 240, 183));

    private static readonly Brush P05AttentionHoverBrush = P05Freeze(Color.FromRgb(160, 105, 20));
    private static readonly Brush P05AttentionHoverBorderBrush = P05Freeze(Color.FromRgb(255, 211, 109));

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
        if (sender is Button button)
            ApplyP05Hover(button);
    }

    private void P05Button_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Button button)
            RestoreP05Chrome(button);
    }

    private static bool IsP05HardwareButton(Button? button)
    {
        if (button is null)
            return false;

        var tag = button.Tag?.ToString()?.ToUpperInvariant();
        if (tag is "ENTER" or "LEFT" or "RIGHT" or "BACK" or "HOME" or "MENU" or
            "REMOTE_ACTIVE" or "AUTO_ACTIVE" or "LOCAL_ACTIVE" or "MANUAL_ACTIVE")
            return true;

        // Inactive authority/mode buttons intentionally have no tag because the
        // P04 illumination layer reserves tags for the active electrical state.
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

        // Local values on the template chrome deliberately outrank the legacy
        // IsMouseOver trigger which used a near-white background. This keeps white
        // text / Lucide icons readable and preserves the semantic active-mode color.
        chrome.Background = background;
        chrome.BorderBrush = border;
    }

    private static void RestoreP05Chrome(Button button)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("Chrome", button) is not Border chrome)
            return;

        chrome.ClearValue(Border.BackgroundProperty);
        chrome.ClearValue(Border.BorderBrushProperty);
    }

    private static Brush P05Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
