using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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

            // This catches any Lucide content that already exists. P0 can replace the
            // bezel content later during Loaded, so MouseEnter repeats this binding lazily.
            BindP05LucideForeground(button);
        }
    }

    private static void BindP05LucideForeground(Button button)
    {
        if (button.Content is not LucideIcon icon)
            return;

        BindingOperations.SetBinding(
            icon,
            LucideIcon.ForegroundProperty,
            new Binding(nameof(Control.Foreground))
            {
                Source = button,
                Mode = BindingMode.OneWay
            });
    }

    private void P05Button_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button button)
            return;

        // P0 replaces the original bezel content with LucideIcon after P05 can already
        // have been installed. Bind at the actual interaction point, when the final icon
        // is guaranteed to exist, then change the button foreground to the dark legend.
        BindP05LucideForeground(button);
        ApplyP05BrightHover(button);

        // Re-check once at Render priority as well. This handles any late ContentPresenter
        // refresh without racing the white-hover frame: the final Lucide instance is bound
        // directly to Button.Foreground before the frame is presented.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                if (!button.IsMouseOver)
                    return;

                BindP05LucideForeground(button);
                ApplyP05BrightHover(button);
            }));
    }

    private static void ApplyP05BrightHover(Button button)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("Chrome", button) is Border chrome)
        {
            chrome.Background = P05FlashSurfaceBrush;
            chrome.BorderBrush = P05FlashBorderBrush;
        }

        button.Foreground = P05FlashTextBrush;

        // Force an immediate binding target refresh for the final bezel icon. Normally
        // the dependency-property notification is synchronous, but this makes the intent
        // deterministic across ContentPresenter/template rebuilds.
        if (button.Content is LucideIcon icon)
            BindingOperations.GetBindingExpression(icon, LucideIcon.ForegroundProperty)?.UpdateTarget();
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

        // Text buttons fall back to DeviceButton styling; LOCAL/REMOTE/AUTO/MANUAL are
        // immediately repainted by P04 below. Bezel Lucide icons remain bound directly
        // to the Button.Foreground and therefore return to the light normal foreground.
        button.ClearValue(Control.ForegroundProperty);
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