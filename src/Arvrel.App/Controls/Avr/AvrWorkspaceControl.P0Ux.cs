using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private AvrP0HmiControl? _p0Hmi;
    private bool _p0HmiInstalled;
    private bool _p0RefreshAttached;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += P0NativeHmi_Loaded;
        Unloaded += P0NativeHmi_Unloaded;
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

        _p0Hmi = new AvrP0HmiControl();
        _p0Hmi.StartServerRequested += P0StartServerRequested;
        _p0Hmi.StopServerRequested += P0StopServerRequested;
        displayBorder.Child = _p0Hmi;
        displayBorder.Background = new SolidColorBrush(Color.FromRgb(243, 245, 246));
        displayBorder.Padding = new Thickness(0);

        RewireP0NativeHardwareKeys();
        _p0HmiInstalled = true;
        if (!_p0RefreshAttached)
        {
            _timer.Tick += P0NativeHmi_Tick;
            _p0RefreshAttached = true;
        }

        RefreshP0NativeHmi();
        AddEvent("HMI", "Native P0 AVR display loaded");
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

    private void RewireP0NativeHardwareKeys()
    {
        foreach (var button in FindVisualChildren<Button>(this))
        {
            var tag = button.Tag?.ToString()?.ToUpperInvariant();
            if (tag is not ("ENTER" or "LEFT" or "RIGHT" or "BACK" or "MENU"))
                continue;

            button.Click -= DeviceFunction_Click;
            button.Click -= HmiHardwareNavigation_Click;
            button.Click -= P0NativeHardwareKey_Click;
            button.Click += P0NativeHardwareKey_Click;
        }
    }

    private void P0NativeHardwareKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || _p0Hmi is null)
            return;

        var key = button.Tag?.ToString()?.ToUpperInvariant() ?? string.Empty;
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