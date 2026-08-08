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

            BindP05LucideForeground(button);
        }
    }

    private static void BindP05LucideForeground(Button button)
    {
        // CreateNativeLucideIcon historically assigns Foreground=White as a local value.
        // Replace that local value with a one-way binding to the containing button. The
        // button's Foreground is therefore the single source of truth for normal, active,
        // disabled and bright-hover states; no event-order race can leave a white glyph
        // on the near-white hover face.
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

        ApplyP05BrightHover(button);

        // Re-assert the button state once at Render priority in case the legacy template
        // re-evaluates IsMouseOver after MouseEnter. Lucide glyphs follow automatically
        // through the binding above.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                if (button.IsMouseOver)
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
        // immediately repainted by P04 below. Bezel Lucide icons require no separate
        // restore because their foreground is bound directly to this button property.
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