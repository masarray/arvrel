using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private const double PhysicalFaceplateWidth = 680.0;
    private const int DefaultMinimumTap = 1;
    private const int DefaultMaximumTap = 17;
    private const int DefaultNeutralTap = 9;

    private AvrP0HmiControl? _p0Hmi;
    private bool _p0HmiInstalled;
    private bool _p0RefreshAttached;
    private string? _lastPointerHardwareKey;
    private long _lastPointerHardwareDispatchMs;

    protected override void OnInitialized(EventArgs e)
    {
        ApplyTransformerOperatingDefaults();
        base.OnInitialized(e);
        Loaded += P0NativeHmi_Loaded;
        Unloaded += P0NativeHmi_Unloaded;

        // Front-panel keys must behave like physical keys, independent of the
        // legacy Click handler or which LCD child is currently hosted. Pointer
        // input is intercepted on the tunnelling route before child handlers.
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(P0FrontPanelPreviewMouseDown), handledEventsToo: true);
        // Retain Click routing for keyboard activation / accessibility. A short
        // duplicate guard prevents one physical mouse press dispatching twice.
        AddHandler(Button.ClickEvent, new RoutedEventHandler(P0FrontPanelNavigation_Click), handledEventsToo: true);
    }

    private void ApplyTransformerOperatingDefaults()
    {
        _settings = _settings with
        {
            ProfileName = "ARV-AVR · 17-position OLTC",
            MinimumTap = DefaultMinimumTap,
            MaximumTap = DefaultMaximumTap,
            NeutralTap = DefaultNeutralTap,
            VoltageInputMode = AvrVoltageInputMode.ClosedLoopPlant
        };

        _engine.ApplySettings(_settings, keepTapPosition: false);
        _snapshot = AvrSnapshot.Ready(_settings, _settings.NeutralTap);
        _lastTapPosition = _settings.NeutralTap;
        _lastPendingTapPosition = null;
        _lastState = AvrControlState.SourceOff;
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

        LockPhysicalFaceplateGeometry(displayBorder);
        SetConfigurationExpanded(false);

        _p0Hmi = new AvrP0HmiControl
        {
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
        AddEvent("HMI", $"Operational AVR HMI loaded · tap {_settings.NeutralTap:00}/{_settings.MaximumTap:00} neutral · simulated transformer default");
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

    private void P0FrontPanelPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        var button = FindAncestorButton(e.OriginalSource as DependencyObject);
        var key = HardwareKey(button);
        if (key is null)
            return;

        DispatchHardwareKey(key);
        _lastPointerHardwareKey = key;
        _lastPointerHardwareDispatchMs = Environment.TickCount64;
        // Do not mark the mouse event handled: the physical button should retain
        // its normal pressed visual. The following Click is duplicate-guarded.
    }

    private void P0FrontPanelNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button)
            return;

        var key = HardwareKey(button);
        if (key is null)
            return;

        var duplicatePointerClick = string.Equals(key, _lastPointerHardwareKey, StringComparison.Ordinal) &&
                                    Environment.TickCount64 - _lastPointerHardwareDispatchMs < 500;
        if (!duplicatePointerClick)
            DispatchHardwareKey(key);

        _lastPointerHardwareKey = null;
        e.Handled = true;
    }

    private void DispatchHardwareKey(string key)
    {
        if (_p0Hmi is null)
            InstallP0NativeHmi();

        if (_p0Hmi is null)
        {
            AddEvent("KEY ERR", $"Front-panel {key} ignored · HMI unavailable");
            return;
        }

        AddEvent("KEY", $"Front-panel {key} pressed");
        _p0Hmi.HandleHardwareKey(key);
    }

    private static string? HardwareKey(Button? button)
    {
        var key = button?.Tag?.ToString()?.ToUpperInvariant();
        return key is "ENTER" or "LEFT" or "RIGHT" or "BACK" or "MENU" ? key : null;
    }

    private static Button? FindAncestorButton(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Button button)
                return button;

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
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
        _p0Hmi.ApplyTransformerConvention(_snapshot, _settings);
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