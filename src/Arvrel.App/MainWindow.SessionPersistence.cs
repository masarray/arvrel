using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Services;
using Arvrel.ProcessBus;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _sessionPersistenceInitialized;
    private bool _restoringSessionPreferences;
    private object? _observedAdapterItemsSource;
    private UserPreferencesStore? _userPreferencesStore;
    private UserPreferences _userPreferences = new();

    internal void InitializeSessionPersistence()
    {
        if (_sessionPersistenceInitialized)
            return;

        _sessionPersistenceInitialized = true;
        _userPreferencesStore = new UserPreferencesStore();
        _userPreferences = _userPreferencesStore.Load();

        AdapterCombo.DropDownClosed += (_, _) => PersistCurrentAdapter();
        AdapterCombo.LayoutUpdated += (_, _) => RestoreAdapterAfterItemsRefresh();
        RunButton.Click += (_, _) => Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(PersistCurrentAdapter));
        Closing += (_, _) => PersistCurrentSession();

        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                AttachSclPersistenceHook();
                RestoreSessionPreferences();
            }));
    }

    private void RestoreSessionPreferences()
    {
        if (_userPreferencesStore is null)
            return;

        _restoringSessionPreferences = true;
        try
        {
            RestoreLastAdapter();
            RestoreLastScl();
        }
        finally
        {
            _restoringSessionPreferences = false;
        }
    }

    private void RestoreAdapterAfterItemsRefresh()
    {
        var currentSource = AdapterCombo.ItemsSource;
        if (ReferenceEquals(currentSource, _observedAdapterItemsSource))
            return;

        _observedAdapterItemsSource = currentSource;
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                _restoringSessionPreferences = true;
                try
                {
                    RestoreLastAdapter();
                }
                finally
                {
                    _restoringSessionPreferences = false;
                }
            }));
    }

    private void RestoreLastAdapter()
    {
        var adapters = AdapterCombo.ItemsSource?.Cast<ProcessBusAdapter>().ToArray()
            ?? Array.Empty<ProcessBusAdapter>();
        if (adapters.Length == 0)
            return;

        var selected = adapters.FirstOrDefault(adapter =>
                IsUsableMac(_userPreferences.LastAdapterMacAddress) &&
                string.Equals(adapter.MacAddress, _userPreferences.LastAdapterMacAddress, StringComparison.OrdinalIgnoreCase))
            ?? adapters.FirstOrDefault(adapter =>
                !string.IsNullOrWhiteSpace(_userPreferences.LastAdapterDisplayName) &&
                string.Equals(adapter.DisplayName, _userPreferences.LastAdapterDisplayName, StringComparison.OrdinalIgnoreCase))
            ?? adapters.FirstOrDefault(adapter =>
                !string.IsNullOrWhiteSpace(_userPreferences.LastAdapterSelector) &&
                string.Equals(adapter.Selector, _userPreferences.LastAdapterSelector, StringComparison.Ordinal));

        if (selected is not null)
            AdapterCombo.SelectedItem = selected;
    }

    private void RestoreLastScl()
    {
        if (!SmvProcessBusController.IsAvailable || string.IsNullOrWhiteSpace(_userPreferences.LastSclPath))
            return;

        var path = _userPreferences.LastSclPath;
        if (!File.Exists(path))
        {
            _userPreferences = _userPreferences with { LastSclPath = null };
            _userPreferencesStore?.TrySave(_userPreferences);
            SclStatusText.Text = "SAVED SCL MISSING";
            AddEvent("SCL", "Saved SCL path no longer exists");
            return;
        }

        try
        {
            _processBus.LoadScl(path);
            _loadedSclPath = path;
            SclStatusText.Text = _processBus.SclSummary.ToUpperInvariant();
            StatusText.Text = $"Restored last SCL · {_processBus.SclSummary}.";
            AddEvent("SCL", $"Restored {Path.GetFileName(path)}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            _userPreferences = _userPreferences with { LastSclPath = null };
            _userPreferencesStore?.TrySave(_userPreferences);
            SclStatusText.Text = "SCL RESTORE FAILED";
            AddEvent("SCL ERROR", ex.Message);
        }
    }

    private void AttachSclPersistenceHook()
    {
        var button = SessionVisualDescendants<Button>(this)
            .FirstOrDefault(candidate => string.Equals(
                candidate.ToolTip?.ToString(),
                "Import IEC 61850 SCL",
                StringComparison.Ordinal));
        if (button is null)
            return;

        button.Click += (_, _) => Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(PersistLoadedScl));
    }

    private void PersistLoadedScl()
    {
        if (_restoringSessionPreferences || string.IsNullOrWhiteSpace(_loadedSclPath))
            return;

        _userPreferences = _userPreferences with { LastSclPath = _loadedSclPath };
        _userPreferencesStore?.TrySave(_userPreferences);
    }

    private void PersistCurrentAdapter()
    {
        if (_restoringSessionPreferences || AdapterCombo.SelectedItem is not ProcessBusAdapter adapter)
            return;

        _userPreferences = _userPreferences with
        {
            LastAdapterSelector = adapter.Selector,
            LastAdapterDisplayName = adapter.DisplayName,
            LastAdapterMacAddress = IsUsableMac(adapter.MacAddress) ? adapter.MacAddress : null
        };
        _userPreferencesStore?.TrySave(_userPreferences);
    }

    private void PersistCurrentSession()
    {
        PersistCurrentAdapter();
        PersistLoadedScl();
    }

    private static bool IsUsableMac(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           !string.Equals(value, "No MAC", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<T> SessionVisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;

            foreach (var descendant in SessionVisualDescendants<T>(child))
                yield return descendant;
        }
    }
}

internal static class SessionPersistenceBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoaded));
    }

    private static void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.InitializeSessionPersistence();
    }
}
