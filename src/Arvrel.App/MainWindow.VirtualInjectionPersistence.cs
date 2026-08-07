using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Arvrel.Application.Laboratory;
using Microsoft.Win32;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _virtualInjectionPersistenceInitialized;

    internal void InitializeVirtualInjectionPersistence()
    {
        if (_virtualInjectionPersistenceInitialized)
            return;
        if (!_virtualInjectionInitialized || _virtualInjectionView is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(InitializeVirtualInjectionPersistence));
            return;
        }

        var clearButton = FindButtonByContent(_virtualInjectionView, "Clear injection");
        if (clearButton?.Parent is not StackPanel actions)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(InitializeVirtualInjectionPersistence));
            return;
        }

        _virtualInjectionPersistenceInitialized = true;
        var loadButton = CreateProfileButton(
            "Load profile",
            "Load a versioned ARVREL virtual-injection profile. Invalid files leave the active profile unchanged.");
        loadButton.Click += LoadVirtualInjectionProfile_Click;
        actions.Children.Insert(0, loadButton);

        var saveButton = CreateProfileButton(
            "Save profile",
            "Atomically save the reproducible injection and CT configuration. Runtime flux, source time, and relay state are excluded.");
        saveButton.Margin = new Thickness(0, 0, 5, 0);
        saveButton.Click += SaveVirtualInjectionProfile_Click;
        actions.Children.Insert(1, saveButton);
    }

    private Button CreateProfileButton(string content, string toolTip)
        => new()
        {
            Style = FindResource("CompactButton") as Style,
            Content = content,
            Margin = new Thickness(0, 0, 5, 0),
            ToolTip = toolTip
        };

    private void SaveVirtualInjectionProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = _scenario.ActiveProfile;
        var dialog = new SaveFileDialog
        {
            Title = "Save ARVREL virtual-injection profile",
            Filter = "ARVREL injection profile (*.arvrel-injection.json)|*.arvrel-injection.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".arvrel-injection.json",
            AddExtension = true,
            FileName = $"{SafeProfileFileName(profile.Name)}.arvrel-injection.json"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            VirtualInjectionProfilePersistence.SaveAtomic(
                dialog.FileName,
                profile,
                "ARVREL public WPF virtual-injection editor");
            AddEvent("PROFILE SAVE", System.IO.Path.GetFileName(dialog.FileName));
            StatusText.Text = $"Virtual-injection profile saved atomically to {dialog.FileName}. Runtime CT and relay state were not persisted.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Profile save failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = ex.Message;
        }
    }

    private void LoadVirtualInjectionProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load ARVREL virtual-injection profile",
            Filter = "ARVREL injection profile (*.arvrel-injection.json)|*.arvrel-injection.json|JSON (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            // Parse, validate, normalize, and verify the fingerprint before mutating
            // the active laboratory. A rejected file therefore retains last-valid state.
            var loaded = VirtualInjectionProfilePersistence.LoadFile(dialog.FileName);
            var wasRunning = _scenario.IsRunning;
            _scenario.ApplyProfile(loaded.Profile);
            _scenario.Restart(keepProfile: true);
            if (wasRunning)
                _scenario.StartInjection();
            SyncVirtualInjectionEditorFromProfile(loaded.Profile);
            UpdateVirtualInjectionProvenance();

            var rebuilding = wasRunning;
            SetVirtualInjectionStatus(
                rebuilding ? "PROFILE LOADED · REBUILDING" : "PROFILE LOADED",
                rebuilding ? WarningBrush : HealthyBrush,
                rebuilding ? "#FBF2E3" : "#EAF5EC",
                rebuilding ? "#E2C58F" : "#B9D8BF");
            RenderInitialFrame();
            RefreshPhasorFrame();
            RefreshCtObservability();

            var migration = loaded.Migrated ? " · migrated legacy schema 0" : string.Empty;
            AddEvent("PROFILE LOAD", $"{loaded.Profile.Name} · {loaded.Fingerprint[..12]}{migration}");
            StatusText.Text = $"Profile '{loaded.Profile.Name}' loaded and fingerprint-verified{migration}. Transient CT, source-time, and relay state were not restored.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            MessageBox.Show(
                this,
                $"The selected file was not applied. The last valid injection remains active.\n\n{ex.Message}",
                "Profile load rejected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            SetVirtualInjectionStatus("LOAD REJECTED · LAST VALID ACTIVE", TripBrush, "#FCEAEA", "#E5B6B3");
            StatusText.Text = ex.Message;
        }
    }

    private static Button? FindButtonByContent(DependencyObject root, string content)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is Button button && string.Equals(button.Content as string, content, StringComparison.Ordinal))
                return button;
            var nested = FindButtonByContent(child, content);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private static string SafeProfileFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "ARVREL-injection-profile" : safe;
    }
}

internal static class VirtualInjectionPersistenceBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(window.InitializeVirtualInjectionPersistence));
    }
}
