using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Arvrel.App;

public partial class MainWindow
{
    internal void ResetWaveformEvidenceMarkerLocks()
        => SmvScope.ResetEvidenceMarkerLocks();
}

internal static class WaveformEvidenceResetBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // WPF class handlers run before instance SelectionChanged handlers at the
        // ComboBox source. Reset the scope synchronously before MainWindow renders
        // a newly selected source/stream that may already contain finite evidence.
        EventManager.RegisterClassHandler(
            typeof(ComboBox),
            Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler(OnComboBoxSelectionChanged));
    }

    private static void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo ||
            combo.Name is not ("SourceCombo" or "StreamCombo") ||
            Window.GetWindow(combo) is not MainWindow window ||
            !window.IsLoaded)
        {
            return;
        }

        // During InitializeComponent, SourceCombo/StreamCombo can raise SelectionChanged
        // before the named SmvScope field has been created. The runtime cursor-reset path
        // is only required after the window is loaded, when all named controls exist.
        window.ResetWaveformEvidenceMarkerLocks();
    }
}
