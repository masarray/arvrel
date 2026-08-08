using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Arvrel.App.Controls;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private bool _p05HoverInstalled;

    // Physical push-button hover: a short bright face with dark legend, matching the
    // existing RAISE/LOWER interaction. The semantic lamp colour returns on mouse leave.
    private static readonly Brush P05FlashSurfaceBrush = P05Freeze(Color.FromRgb(239, 245, 247));
    private static readonly Brush P05FlashBorderBrush = P05Freeze(Color.FromRgb(255, 255, 255));
    private static readonly Brush P05FlashTextBrush = P05Freeze(Color.FromRgb(28, 48, 57));

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

        button.ApplyTemplate();
        if (button.Template.FindName("Chrome", button) is Border chrome)
        {
            // Intentionally bright like a physical illuminated/pressed key, but never
            // white-on-white: caption and Lucide glyph are changed to a dark legend.
            chrome.Background = P05FlashSurfaceBrush;
            chrome.BorderBrush = P05FlashBorderBrush;
        }

        button.Foreground = P05FlashTextBrush;
        if (button.Content is LucideIcon icon)
            icon.Foreground = P05FlashTextBrush;
    }

    private void P05Button_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Button button)
            return;

        button.ApplyTemplate();
        if (button.Template.FindName("Chrome", button) is Border chrome)
        {
            // IsMouseOver is already false here. Clearing the local hover values lets
            // DeviceButton return to its real normal/disabled/semantic material state.
            chrome.ClearValue(Border.BackgroundProperty);
            chrome.ClearValue(Border.BorderBrushProperty);
        }

        if (button.Content is LucideIcon icon)
            icon.ClearValue(LucideIcon.ForegroundProperty);

        RefreshP04ModeVisuals();
    }

    private static bool IsP05HardwareButton(Button? button)
    {
        if (button is null)
            return false;

        var tag = button.Tag?.ToString()?.ToUpperInvariant();
        if (tag is "ENTER" or "LEFT" or "RIGHT" or "BACK" or "HOME" or "MENU" or
            "REMOTE_ACTIVE" or "AUTO_ACTIVE" or "LOCAL_ACTIVE" or "MANUAL_ACTIVE")
            return true;

        if (button.Name is "LocalAuthorityButton" or "RemoteAuthorityButton" or
            "AutoModeButton" or "ManualModeButton")
            return true;

        // RAISE/LOWER intentionally have no x:Name in the legacy faceplate XAML.
        var caption = button.Content?.ToString()?.ToUpperInvariant();
        return caption?.Contains("RAISE", StringComparison.Ordinal) == true ||
               caption?.Contains("LOWER", StringComparison.Ordinal) == true;
    }

    private static Brush P05Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
