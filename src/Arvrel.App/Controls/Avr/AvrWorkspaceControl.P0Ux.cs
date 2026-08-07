using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private const double PhysicalFaceplateWidth = 680.0;

    private AvrP0HmiControl? _p0Hmi;
    private bool _p0HmiInstalled;
    private bool _p0RefreshAttached;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += P0NativeHmi_Loaded;
        Unloaded += P0NativeHmi_Unloaded;

        // Hardware navigation is a bubbled Button.Click route. Owning the route
        // here is deterministic even when the LCD child is replaced at runtime.
        // Do not depend on visual-tree button discovery / Loaded ordering.
        AddHandler(Button.ClickEvent, new RoutedEventHandler(P0FrontPanelNavigation_Click), handledEventsToo: true);
    }

    private void P0NativeHmi_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(InstallP0NativeHmi));
    }

    private void P0NativeHmi_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_p0RefreshAttached)
        {
            _timer.Tick -= P0NativeHmi_Tick;
            _p0RefreshAttached = false;
        }
    }

    private void InstallP0NativeHmi()
    {
        if (_p0HmiInstalled)
            return;

        var displayBorder = FindLegacyHmiDisplayBorder();
        if (displayBorder is null)
        {
            AddEvent("HMI", "P0 display host not found");
            return;
        }

        // Device geometry is physical, not a responsive dashboard. The workspace
        // can gain/lose room when the configuration rail changes, but the AVR
        // chassis itself must retain the same width like a real panel device.
        LockPhysicalFaceplateGeometry(displayBorder);
        SetConfigurationExpanded(false);

        _p0Hmi = new AvrP0HmiControl
        {
            // The native HMI must reflow inside the fixed chassis instead of
            // forcing its parent to become wider.
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _p0Hmi.StartServerRequested += P0StartServerRequested;
        _p0Hmi.StopServerRequested += P0StopServerRequested;
        displayBorder.Child = _p0Hmi;
        displayBorder.Background = new SolidColorBrush(Color.FromRgb(243, 245, 246));
        displayBorder.Padding = new Thickness(0);

        _p0HmiInstalled = true;
        if (!_p0RefreshAttached)
        {
            _timer.Tick += P0NativeHmi_Tick;
            _p0RefreshAttached = true;
        }

        RefreshP0NativeHmi();
        AddEvent("HMI", $"Operational AVR HMI loaded · fixed chassis {PhysicalFaceplateWidth:0}px");
    }

    private static void LockPhysicalFaceplateGeometry(Border displayBorder)
    {
        DependencyObject? current = displayBorder;
        while (current is not null)
        {
            if (current is Border border && Grid.GetRow(border) == 1 && Grid.GetColumn(border) == 2)
            {
                border.Width = PhysicalFaceplateWidth;
                border.MinWidth = PhysicalFaceplateWidth;
                border.MaxWidth = PhysicalFaceplateWidth;
                border.HorizontalAlignment = HorizontalAlignment.Center;
                border.VerticalAlignment = VerticalAlignment.Stretch;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private Border? FindLegacyHmiDisplayBorder()
    {
        DependencyObject? current = TrendCanvas;
        while (current is not null)
        {
            if (current is Border border && border.BorderThickness.Left >= 3)
                return border;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void P0FrontPanelNavigation_Click(object sender, RoutedEventArgs e)
    {
        // Button.Click bubbles with the Button as Source. This survives all LCD
        // replacement/retemplating and does not rely on discovering the buttons.
        if (e.Source is not Button button)
            return;

        var key = button.Tag?.ToString()?.ToUpperInvariant();
        if (key is not ("ENTER" or "LEFT" or "RIGHT" or "BACK" or "MENU"))
            return;

        // In the unlikely event of an early click during initial Loaded dispatch,
        // install the HMI synchronously before forwarding the hardware key.
        if (_p0Hmi is null)
            InstallP0NativeHmi();
        if (_p0Hmi is null)
        {
            AddEvent("KEY ERR", $"Front-panel {key} ignored · HMI unavailable");
            e.Handled = true;
            return;
        }

        AddEvent("KEY", $"Front-panel {key} pressed");
        _p0Hmi.HandleHardwareKey(key);
        e.Handled = true;
    }

    private void P0NativeHmi_Tick(object? sender, EventArgs e) => RefreshP0NativeHmi();

    private void RefreshP0NativeHmi()
    {
        if (_p0Hmi is null)
            return;

        _p0Hmi.UpdateState(
            _snapshot,
            _settings,
            _frequencyHz,
            _events.ToArray(),
            _iec61850Server.GetStatus());
    }

    private void P0StartServerRequested(string host, int port)
    {
        try
        {
            _iec61850Server.Start(host, port);
            AddEvent("IEC61850", $"MMS server started · {host}:{port}");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or SocketException or FormatException)
        {
            AddEvent("IEC ERR", ex.Message);
            ShowWarning(ex.Message, "AVR IEC 61850 server");
        }
        finally
        {
            RefreshP0NativeHmi();
        }
    }

    private async void P0StopServerRequested()
    {
        try
        {
            await _iec61850Server.StopAsync();
            AddEvent("IEC61850", "MMS server stopped");
        }
        catch (Exception ex) when (ex is InvalidOperationException or SocketException)
        {
            AddEvent("IEC ERR", ex.Message);
        }
        finally
        {
            RefreshP0NativeHmi();
        }
    }
}