using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _ctModelSelectorInitialized;
    private bool _ctModelSelectorSync;
    private ComboBox? _ctModelPresetCombo;

    internal void InitializeCtModelSelector()
    {
        if (_ctModelSelectorInitialized)
            return;
        if (!_virtualInjectionInitialized ||
            !_virtualInjectionPersistenceInitialized ||
            _virtualInjectionView is null ||
            _virtualInjectionPresetCombo is null ||
            _virtualInjectionFrequencyText is null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(InitializeCtModelSelector));
            return;
        }

        var toolbar = _virtualInjectionView.Children
            .OfType<Grid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 0);
        if (toolbar is null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(InitializeCtModelSelector));
            return;
        }

        _ctModelSelectorInitialized = true;

        // Replace the source-preset handler so a source change does not silently
        // disable or replace the independently selected CT model.
        _virtualInjectionPresetCombo.SelectionChanged -= VirtualInjectionPresetCombo_SelectionChanged;
        _virtualInjectionPresetCombo.ItemsSource = VirtualInjectionPresets.Names
            .Concat(VirtualInjectionCtStudyScenarios.AdditionalNames)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _virtualInjectionPresetCombo.SelectionChanged += CtAwareSourcePreset_SelectionChanged;
        _virtualInjectionPresetCombo.ToolTip =
            "SOURCE PRESET · selects the voltage/current condition. The CT MODEL selector is independent and remains active when ordinary source presets change.";

        var selector = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 98, 0)
        };
        Grid.SetColumn(selector, 5);
        toolbar.Children.Add(selector);

        selector.Children.Add(new TextBlock
        {
            Text = "CT MODEL",
            Style = FindResource("SectionLabel") as Style,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Current-transformer model applied independently of the selected source/fault preset."
        });

        _ctModelPresetCombo = new ComboBox
        {
            ItemsSource = VirtualInjectionCtModelPresets.Names,
            Width = 176,
            Height = 28,
            MinHeight = 28,
            MaxHeight = 28,
            Padding = new Thickness(7, 0, 7, 0),
            FontSize = 9.4,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Choose ideal pass-through or a nonlinear protective-CT study model. This selection survives ordinary source-preset changes."
        };
        _ctModelPresetCombo.SelectionChanged += CtModelPresetCombo_SelectionChanged;
        selector.Children.Add(_ctModelPresetCombo);

        ReplaceClearSourceButton();
        SyncCtModelSelectorFromProfile(_scenario.ActiveProfile);
    }

    internal void SyncCtModelSelectorFromProfile(VirtualInjectionProfile profile)
    {
        if (!_ctModelSelectorInitialized || _ctModelPresetCombo is null)
            return;

        var resolved = VirtualInjectionCtModelPresets.ResolveName(profile.CurrentTransformer);
        _ctModelSelectorSync = true;
        try
        {
            _ctModelPresetCombo.SelectedItem = resolved;
            _ctModelPresetCombo.ToolTip = resolved is null
                ? CtModelSummary("Custom / loaded CT", profile.CurrentTransformer)
                : CtModelSummary(resolved, profile.CurrentTransformer);
        }
        finally
        {
            _ctModelSelectorSync = false;
        }
    }

    private void CtAwareSourcePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_virtualInjectionEditorSync ||
            _ctModelSelectorSync ||
            _virtualInjectionPresetCombo?.SelectedItem is not string preset)
            return;

        var frequency = ResolveCtAwarePresetFrequency();
        var activeCt = _scenario.ActiveProfile.CurrentTransformer;
        var profile = VirtualInjectionCtStudyScenarios.Contains(preset)
            ? VirtualInjectionCtStudyScenarios.Create(preset, frequency)
            : (VirtualInjectionPresets.Create(preset, frequency) with
            {
                CurrentTransformer = activeCt
            }).Normalize();

        ApplyCtAwareSourceProfile(profile, preset);
    }

    private void CtModelPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ctModelSelectorSync ||
            _virtualInjectionEditorSync ||
            _ctModelPresetCombo?.SelectedItem is not string modelName)
            return;

        var settings = VirtualInjectionCtModelPresets.Create(modelName);
        var profile = (_scenario.ActiveProfile with
        {
            CurrentTransformer = settings
        }).Normalize();
        var changed = _scenario.ApplyProfile(profile);
        SyncCtModelSelectorFromProfile(profile);

        if (changed)
        {
            SetVirtualInjectionStatus("CT MODEL · REBUILDING", WarningBrush, "#FBF2E3", "#E2C58F");
            AddEvent("CT MODEL", $"{modelName} · {profile.Fingerprint()[..12]}");
            StatusText.Text = settings.Enabled
                ? $"CT model '{modelName}' applied independently of source preset '{profile.Name}'. One coherent cycle is rebuilding."
                : $"CT nonlinear model disabled. Source preset '{profile.Name}' remains unchanged.";
        }

        RenderInitialFrame();
        RefreshPhasorFrame();
        RefreshCtObservability();
    }

    private double ResolveCtAwarePresetFrequency()
    {
        var frequency = _scenario.ActiveProfile.FrequencyHz;
        if (_virtualInjectionFrequencyText is not null &&
            VirtualInjectionRow.TryParseEngineeringDouble(_virtualInjectionFrequencyText.Text, out var entered) &&
            entered is >= VirtualInjectionProfile.MinimumFrequencyHz and <= VirtualInjectionProfile.MaximumFrequencyHz)
        {
            frequency = entered;
        }
        return frequency;
    }

    private void ApplyCtAwareSourceProfile(VirtualInjectionProfile profile, string preset)
    {
        var changed = _scenario.ApplyProfile(profile);
        SyncVirtualInjectionEditorFromProfile(profile);
        PreserveExtendedPresetSelection(preset);
        SyncCtModelSelectorFromProfile(profile);

        if (changed)
        {
            SetVirtualInjectionStatus("APPLIED · REBUILDING", WarningBrush, "#FBF2E3", "#E2C58F");
            AddEvent("INJECTION", $"Preset {preset} · {profile.Fingerprint()[..12]}");
        }

        RenderInitialFrame();
        RefreshPhasorFrame();
        RefreshCtObservability();
        StatusText.Text = profile.CurrentTransformer.Enabled
            ? $"Source preset '{preset}' applied with CT model '{VirtualInjectionCtModelPresets.ResolveName(profile.CurrentTransformer) ?? "Custom / loaded"}'."
            : $"Source preset '{preset}' applied with ideal CT pass-through.";
    }

    private void PreserveExtendedPresetSelection(string preset)
    {
        if (_virtualInjectionPresetCombo is null ||
            VirtualInjectionPresets.Names.Contains(preset, StringComparer.Ordinal))
            return;

        _virtualInjectionEditorSync = true;
        try
        {
            _virtualInjectionPresetCombo.SelectedItem = preset;
        }
        finally
        {
            _virtualInjectionEditorSync = false;
        }
    }

    private void ReplaceClearSourceButton()
    {
        if (_virtualInjectionView is null)
            return;

        var original = FindButtonByContent(_virtualInjectionView, "Clear injection");
        if (original?.Parent is not StackPanel actions)
            return;

        var index = actions.Children.IndexOf(original);
        if (index < 0)
            return;

        var replacement = new Button
        {
            Style = original.Style,
            Content = "Clear source",
            Margin = original.Margin,
            Padding = original.Padding,
            MinWidth = original.MinWidth,
            Height = original.Height,
            ToolTip = "Return voltage/current source to Normal balanced while preserving the selected CT MODEL."
        };
        replacement.Click += (_, _) =>
        {
            var source = VirtualInjectionPresets.Create("Normal balanced", ResolveCtAwarePresetFrequency());
            var profile = (source with
            {
                CurrentTransformer = _scenario.ActiveProfile.CurrentTransformer
            }).Normalize();
            ApplyCtAwareSourceProfile(profile, "Normal balanced");
        };

        actions.Children.RemoveAt(index);
        actions.Children.Insert(index, replacement);
    }

    private static string CtModelSummary(string name, CtSaturationSettings settings)
        => settings.Enabled
            ? FormattableString.Invariant(
                $"{name}\nVk {settings.KneePointVoltageRms:0.###} V RMS · Rct {settings.SecondaryWindingResistanceOhm:0.###} Ω · burden {settings.BurdenResistanceOhm:0.###} Ω + {settings.BurdenInductanceMilliHenries:0.###} mH · rem {settings.RemanencePercent:+0.##;-0.##;0}%")
            : $"{name}\nRelay current uses ideal pass-through; nonlinear CT state is disabled.";
}

internal static class CtModelSelectorBootstrap
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
                new Action(window.InitializeCtModelSelector));
    }
}
