using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.Application.Ied;

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
    private bool _configurationOverlayPrepared;
    private bool _frontPanelKeysStyled;
    private Viewbox? _faceplateFitHost;
    private string? _lastPointerHardwareKey;
    private long _lastPointerHardwareDispatchMs;

    protected override void OnInitialized(EventArgs e)
    {
        ApplyTransformerOperatingDefaults();
        base.OnInitialized(e);
        InstallP04ModeVisuals();
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
        _engine.SetMode(AvrOperatingMode.Automatic);
        _engine.SetAuthority(AvrControlAuthority.Remote);
        _snapshot = _engine.Advance(TimeSpan.Zero, 0, 0, 0, sourceEnergized: false);
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

        InstallUniformFitFaceplate(displayBorder);
        SetConfigurationExpanded(false);
        PrepareConfigurationOverlay();
        StyleCompactFrontPanelKeys();
        InstallNativeConfigurationNetworkTab();
        ApplyP02LeanInjectionUx();

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

        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(StyleCompactFrontPanelKeys));

        RefreshP0NativeHmi();
        RefreshP04ModeVisuals();
        AddEvent("HMI", $"Operational AVR HMI ready · REMOTE · AUTO · tap {_settings.NeutralTap:00}/{_settings.MaximumTap:00} · simulated transformer");
    }

    private void InstallUniformFitFaceplate(Border displayBorder)
    {
        if (VisualTreeHelper.GetParent(displayBorder) is Grid deviceGrid && deviceGrid.ColumnDefinitions.Count >= 5)
        {
            deviceGrid.ColumnDefinitions[1].Width = new GridLength(6);
            deviceGrid.ColumnDefinitions[3].Width = new GridLength(6);
            deviceGrid.ColumnDefinitions[4].Width = new GridLength(48);
        }

        Border? faceplate = null;
        DependencyObject? current = displayBorder;
        while (current is not null)
        {
            if (current is Border border && Grid.GetRow(border) == 1 && Grid.GetColumn(border) == 2)
            {
                faceplate = border;
                break;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        if (faceplate is null)
            return;

        faceplate.Width = PhysicalFaceplateWidth;
        faceplate.Height = PhysicalFaceplateHeight;
        faceplate.MinWidth = PhysicalFaceplateWidth;
        faceplate.MaxWidth = PhysicalFaceplateWidth;
        faceplate.MinHeight = PhysicalFaceplateHeight;
        faceplate.MaxHeight = PhysicalFaceplateHeight;
        faceplate.HorizontalAlignment = HorizontalAlignment.Stretch;
        faceplate.VerticalAlignment = VerticalAlignment.Stretch;

        if (VisualTreeHelper.GetParent(faceplate) is not Grid rootGrid || _faceplateFitHost is not null)
            return;

        var index = rootGrid.Children.IndexOf(faceplate);
        if (index < 0)
            return;

        rootGrid.Children.RemoveAt(index);
        Grid.SetRow(faceplate, 0);
        Grid.SetColumn(faceplate, 0);

        _faceplateFitHost = new Viewbox
        {
            Child = faceplate,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(4)
        };
        Grid.SetRow(_faceplateFitHost, 1);
        Grid.SetColumn(_faceplateFitHost, 2);
        Panel.SetZIndex(_faceplateFitHost, 0);
        rootGrid.Children.Insert(index, _faceplateFitHost);
    }

    private void PrepareConfigurationOverlay()
    {
        if (_configurationOverlayPrepared)
            return;
        if (VisualTreeHelper.GetParent(ConfigurationPanel) is not Grid)
            return;

        _configurationOverlayPrepared = true;
        Grid.SetColumn(ConfigurationPanel, 2);
        Grid.SetColumnSpan(ConfigurationPanel, 3);
        ConfigurationPanel.HorizontalAlignment = HorizontalAlignment.Right;
        ConfigurationPanel.VerticalAlignment = VerticalAlignment.Stretch;
        Panel.SetZIndex(ConfigurationPanel, 50);
        ConfigurationColumn.Width = new GridLength(38);
        UpdateConfigurationOverlayWidth();

        ConfigurationExpanded.IsVisibleChanged += (_, _) => UpdateConfigurationOverlayWidth();
    }

    private void UpdateConfigurationOverlayWidth()
    {
        ConfigurationColumn.Width = new GridLength(38);
        ConfigurationPanel.Width = ConfigurationExpanded.Visibility == Visibility.Visible ? 380 : 38;
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
        var styledCount = 0;
        foreach (var button in FindVisualChildren<Button>(this))
        {
            var raw = button.Tag?.ToString()?.ToUpperInvariant();
            if (raw is not ("ENTER" or "LEFT" or "RIGHT" or "BACK" or "MENU" or "HOME"))
                continue;

            button.Click -= DeviceFunction_Click;

            var key = raw == "BACK" ? "HOME" : raw;
            button.Tag = key;
            button.MinHeight = 34;
            button.Padding = new Thickness(3);
            button.ToolTip = key switch
            {
                "ENTER" => "Enter / confirm",
                "LEFT" => "Previous",
                "RIGHT" => "Next",
                "HOME" => "Home",
                "MENU" => "Menu",
                _ => key
            };

            if (button.Content is not LucideIcon)
                button.Content = CreateNativeLucideIcon(key);
            styledCount++;
        }

        _frontPanelKeysStyled = styledCount >= 5;
    }

    private static LucideIcon CreateNativeLucideIcon(string key)
        => new()
        {
            Kind = key switch
            {
                "ENTER" => LucideIconKind.CornerDownLeft,
                "LEFT" => LucideIconKind.ChevronLeft,
                "RIGHT" => LucideIconKind.ChevronRight,
                "HOME" => LucideIconKind.House,
                "MENU" => LucideIconKind.Menu,
                _ => LucideIconKind.Activity
            },
            Width = 18,
            Height = 18,
            Foreground = Brushes.White
        };

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
            RefreshP04ModeVisuals();
            AddEvent("DEFAULT", "Transformer defaults restored · REMOTE · AUTO · tap 09/17 · simulated plant");
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
        if (!_frontPanelKeysStyled)
            StyleCompactFrontPanelKeys();
        RefreshLeanInjectionUx();
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
        _p0Hmi.ApplyLowFpsTrendSmoothing(_snapshot);
        _p0Hmi.ApplyP04ModeBadges(_snapshot);
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
