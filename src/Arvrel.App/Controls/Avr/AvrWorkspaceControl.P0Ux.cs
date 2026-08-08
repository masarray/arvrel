using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Arvrel.Application.Ied;
using ShapePath = System.Windows.Shapes.Path;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private const double PhysicalFaceplateWidth = 680.0;
    private const double PhysicalFaceplateHeight = 548.0;
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
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(P0FrontPanelPreviewMouseDown), handledEventsToo: true);
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
        StyleCompactFrontPanelKeys();
        InstallNativeConfigurationNetworkTab();

        _p0Hmi = new AvrP0HmiControl
        {
            MinWidth = 0,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _p0Hmi.StartServerRequested += P0StartServerRequested;
        _p0Hmi.StopServerRequested += P0StopServerRequested;
        displayBorder.Child = _p0Hmi;
        displayBorder.Background = new SolidColorBrush(Color.FromRgb(243, 245, 246));
        displayBorder.Padding = new Thickness(0);
        _p0Hmi.ApplyCompactVisualPolish();

        _p0HmiInstalled = true;
        if (!_p0RefreshAttached)
        {
            _timer.Tick += P0NativeHmi_Tick;
            _p0RefreshAttached = true;
        }

        RefreshP0NativeHmi();
        AddEvent("HMI", $"Operational AVR HMI ready · tap {_settings.NeutralTap:00}/{_settings.MaximumTap:00} · simulated transformer");
    }

    private static void LockPhysicalFaceplateGeometry(Border displayBorder)
    {
        if (VisualTreeHelper.GetParent(displayBorder) is Grid deviceGrid && deviceGrid.ColumnDefinitions.Count >= 5)
        {
            deviceGrid.ColumnDefinitions[1].Width = new GridLength(7);
            deviceGrid.ColumnDefinitions[3].Width = new GridLength(7);
            deviceGrid.ColumnDefinitions[4].Width = new GridLength(48);
        }

        DependencyObject? current = displayBorder;
        while (current is not null)
        {
            if (current is Border border && Grid.GetRow(border) == 1 && Grid.GetColumn(border) == 2)
            {
                border.Width = PhysicalFaceplateWidth;
                border.MinWidth = PhysicalFaceplateWidth;
                border.MaxWidth = PhysicalFaceplateWidth;
                border.Height = PhysicalFaceplateHeight;
                border.MinHeight = PhysicalFaceplateHeight;
                border.MaxHeight = PhysicalFaceplateHeight;
                border.HorizontalAlignment = HorizontalAlignment.Center;
                border.VerticalAlignment = VerticalAlignment.Center;
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

    private void StyleCompactFrontPanelKeys()
    {
        foreach (var button in FindVisualChildren<Button>(this))
        {
            var raw = button.Tag?.ToString()?.ToUpperInvariant();
            if (raw is not ("ENTER" or "LEFT" or "RIGHT" or "BACK" or "MENU" or "HOME"))
                continue;

            // The old direct XAML handler logged every navigation click as an AVR
            // event. Remove it; the root routed handler below owns the keypad.
            button.Click -= DeviceFunction_Click;

            var key = raw == "BACK" ? "HOME" : raw;
            button.Tag = key;
            button.MinHeight = 36;
            button.Padding = new Thickness(4);
            button.ToolTip = key switch
            {
                "ENTER" => "Enter / confirm",
                "LEFT" => "Previous",
                "RIGHT" => "Next",
                "HOME" => "Home",
                "MENU" => "Menu",
                _ => key
            };
            button.Content = CreateLucideStyleGlyph(key);
        }
    }

    private static Viewbox CreateLucideStyleGlyph(string key)
    {
        var geometry = key switch
        {
            "ENTER" => "M20,4 L20,9 C20,12 18,14 15,14 L5,14 M9,10 L5,14 9,18",
            "LEFT" => "M15,18 L9,12 15,6",
            "RIGHT" => "M9,18 L15,12 9,6",
            "HOME" => "M3,10 L12,3 21,10 M5,9 L5,20 19,20 19,9 M9,20 L9,14 15,14 15,20",
            "MENU" => "M4,7 L20,7 M4,12 L20,12 M4,17 L20,17",
            _ => "M5,12 L19,12"
        };
        var path = new ShapePath
        {
            Data = Geometry.Parse(geometry),
            Stroke = Brushes.White,
            StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.Uniform
        };
        return new Viewbox { Width = 18, Height = 18, Child = path };
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
    }

    private void P0FrontPanelNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button)
            return;

        if (string.Equals(button.Content?.ToString(), "Restore defaults", StringComparison.OrdinalIgnoreCase))
        {
            ApplyTransformerOperatingDefaults();
            PopulateSettingsUi();
            RenderCurrent();
            AddEvent("DEFAULT", "Transformer defaults restored · tap 09/17 · simulated plant");
            return;
        }

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

        if (key == "HOME")
            _p0Hmi.GoHome();
        else
            _p0Hmi.HandleHardwareKey(key);
    }

    private static string? HardwareKey(Button? button)
    {
        var key = button?.Tag?.ToString()?.ToUpperInvariant();
        return key is "ENTER" or "LEFT" or "RIGHT" or "HOME" or "MENU" ? key : null;
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

    private void P0NativeHmi_Tick(object? sender, EventArgs e)
    {
        RefreshP0NativeHmi();
        RefreshConfigurationNetworkStatus();
    }

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
            RefreshConfigurationNetworkStatus();
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
            RefreshConfigurationNetworkStatus();
        }
    }
}
