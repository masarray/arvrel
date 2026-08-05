using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Arvrel.App.Services;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private DataGrid? _virtualInjectionAngleTable;
    private DataGridColumn? _virtualInjectionAngleColumn;
    private ContextMenu? _virtualInjectionAngleContextMenu;
    private VirtualInjectionRow? _virtualInjectionAngleContextRow;
    private MenuItem? _virtualInjectionBalancedAnglesItem;
    private MenuItem? _virtualInjectionReverseRotationItem;

    private void InitializeVirtualInjectionAngleContextMenu(DataGrid table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _virtualInjectionAngleTable = table;
        _virtualInjectionAngleColumn = table.Columns.FirstOrDefault(column =>
            string.Equals(column.Header?.ToString(), "Angle (°)", StringComparison.Ordinal));
        if (_virtualInjectionAngleColumn is null)
            throw new InvalidOperationException("The virtual-injection angle column was not created.");

        _virtualInjectionAngleContextMenu = BuildVirtualInjectionAngleContextMenu();
        table.ContextMenu = _virtualInjectionAngleContextMenu;
        table.PreviewMouseRightButtonDown += VirtualInjectionTable_PreviewMouseRightButtonDown;
        table.ContextMenuOpening += VirtualInjectionTable_ContextMenuOpening;
        _virtualInjectionAngleContextMenu.Closed += (_, _) => _virtualInjectionAngleContextRow = null;
    }

    private ContextMenu BuildVirtualInjectionAngleContextMenu()
    {
        var menu = new ContextMenu
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
        };
        menu.Items.Add(CreateAngleMenuItem("Zero", (_, _) => ApplySelectedAngleZero()));
        menu.Items.Add(CreateAngleMenuItem("Line Angle", (_, _) => ApplySelectedLineAngle()));

        _virtualInjectionBalancedAnglesItem = CreateAngleMenuItem(
            "Balanced Angles",
            (_, _) => ApplyBalancedAngles());
        menu.Items.Add(_virtualInjectionBalancedAnglesItem);

        menu.Items.Add(new Separator());

        _virtualInjectionReverseRotationItem = CreateAngleMenuItem(
            "Reverse Rotation",
            (_, _) => ApplyReverseRotation());
        menu.Items.Add(_virtualInjectionReverseRotationItem);

        menu.Items.Add(new Separator());
        menu.Items.Add(CreateAngleMenuItem("Copy Table", (_, _) => CopyVirtualInjectionTable()));
        menu.Items.Add(CreateAngleMenuItem("Paste Table", (_, _) => PasteVirtualInjectionTable()));
        return menu;
    }

    private static MenuItem CreateAngleMenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem
        {
            Header = header,
            Padding = new Thickness(10, 5, 18, 5),
            MinWidth = 164
        };
        item.Click += handler;
        return item;
    }

    private void VirtualInjectionTable_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid table || _virtualInjectionAngleColumn is null)
            return;

        var cell = FindVisualAncestor<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell is null || !ReferenceEquals(cell.Column, _virtualInjectionAngleColumn))
        {
            _virtualInjectionAngleContextRow = null;
            return;
        }

        if (cell.DataContext is not VirtualInjectionRow row)
        {
            _virtualInjectionAngleContextRow = null;
            return;
        }

        table.SelectedCells.Clear();
        table.CurrentCell = new DataGridCellInfo(row, cell.Column);
        table.SelectedCells.Add(table.CurrentCell);
        cell.Focus();
        _virtualInjectionAngleContextRow = row;
    }

    private void VirtualInjectionTable_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_virtualInjectionAngleContextRow is null ||
            _virtualInjectionAngleTable?.CurrentCell.Column is null ||
            !ReferenceEquals(_virtualInjectionAngleTable.CurrentCell.Column, _virtualInjectionAngleColumn))
        {
            e.Handled = true;
            return;
        }

        var isThreePhase = VirtualInjectionAngleOperations.IsThreePhaseSignal(_virtualInjectionAngleContextRow.Signal);
        if (_virtualInjectionBalancedAnglesItem is not null)
        {
            _virtualInjectionBalancedAnglesItem.IsEnabled = isThreePhase;
            _virtualInjectionBalancedAnglesItem.ToolTip = isThreePhase
                ? "Balance the selected voltage or current phase group while keeping this cell as the angle anchor."
                : "Balanced Angles requires a three-phase voltage or current row.";
        }
        if (_virtualInjectionReverseRotationItem is not null)
        {
            _virtualInjectionReverseRotationItem.IsEnabled = isThreePhase;
            _virtualInjectionReverseRotationItem.ToolTip = isThreePhase
                ? "Swap L2 and L3 angles in the selected voltage or current group."
                : "Reverse Rotation requires a three-phase voltage or current row.";
        }
    }

    private void ApplySelectedAngleZero()
    {
        if (_virtualInjectionAngleContextRow is null)
            return;

        ApplyVirtualInjectionAngleUpdates(
            new Dictionary<VirtualInjectionSignal, double>
            {
                [_virtualInjectionAngleContextRow.Signal] = 0
            },
            $"{_virtualInjectionAngleContextRow.SignalLabel} angle set to 0°");
    }

    private void ApplySelectedLineAngle()
    {
        if (_virtualInjectionAngleContextRow is null)
            return;

        var angle = VirtualInjectionAngleOperations.StandardLineAngle(_virtualInjectionAngleContextRow.Signal);
        ApplyVirtualInjectionAngleUpdates(
            new Dictionary<VirtualInjectionSignal, double>
            {
                [_virtualInjectionAngleContextRow.Signal] = angle
            },
            $"{_virtualInjectionAngleContextRow.SignalLabel} set to its standard line angle");
    }

    private void ApplyBalancedAngles()
    {
        var selected = _virtualInjectionAngleContextRow;
        if (selected is null || !VirtualInjectionAngleOperations.IsThreePhaseSignal(selected.Signal))
            return;
        if (!VirtualInjectionRow.TryParseEngineeringDouble(selected.AngleText, out var selectedAngle) ||
            !double.IsFinite(selectedAngle))
        {
            SetVirtualInjectionInvalid($"{selected.SignalLabel}: angle must be finite before balancing the phase group.");
            return;
        }

        var updates = VirtualInjectionAngleOperations.BalancedAngles(selected.Signal, selectedAngle);
        ApplyVirtualInjectionAngleUpdates(
            updates,
            $"{selected.Unit} phase angles balanced with {selected.SignalLabel} retained as anchor");
    }

    private void ApplyReverseRotation()
    {
        var selected = _virtualInjectionAngleContextRow;
        if (selected is null || !VirtualInjectionAngleOperations.IsThreePhaseSignal(selected.Signal))
            return;

        var currentAngles = new Dictionary<VirtualInjectionSignal, double>();
        foreach (var signal in VirtualInjectionAngleOperations.PhaseSignals(selected.Signal))
        {
            var row = _virtualInjectionRows.First(candidate => candidate.Signal == signal);
            if (!VirtualInjectionRow.TryParseEngineeringDouble(row.AngleText, out var angle) || !double.IsFinite(angle))
            {
                SetVirtualInjectionInvalid($"{row.SignalLabel}: angle must be finite before reversing phase rotation.");
                return;
            }
            currentAngles[signal] = angle;
        }

        var updates = VirtualInjectionAngleOperations.ReverseRotation(selected.Signal, currentAngles);
        ApplyVirtualInjectionAngleUpdates(
            updates,
            $"{selected.Unit} phase rotation reversed by swapping L2 and L3 angles");
    }

    private void ApplyVirtualInjectionAngleUpdates(
        IReadOnlyDictionary<VirtualInjectionSignal, double> updates,
        string actionDescription)
    {
        ArgumentNullException.ThrowIfNull(updates);
        _virtualInjectionApplyTimer?.Stop();
        _virtualInjectionEditorSync = true;
        try
        {
            foreach (var update in updates)
            {
                var row = _virtualInjectionRows.First(candidate => candidate.Signal == update.Key);
                row.AngleText = VirtualInjectionChannel.NormalizeAngle(update.Value)
                    .ToString("0.###", CultureInfo.InvariantCulture);
            }
            _pendingInjectionName = "Custom injection";
            if (_virtualInjectionPresetCombo is not null)
                _virtualInjectionPresetCombo.SelectedItem = null;
        }
        finally
        {
            _virtualInjectionEditorSync = false;
        }

        if (!TryApplyVirtualInjectionEditor())
            return;

        AddEvent("ANGLE", actionDescription);
        StatusText.Text = $"{actionDescription}. The visible editor was applied atomically through the normal waveform pipeline.";
    }

    private void CopyVirtualInjectionTable()
    {
        if (_virtualInjectionFrequencyText is null ||
            !VirtualInjectionRow.TryParseEngineeringDouble(_virtualInjectionFrequencyText.Text, out var frequency) ||
            !double.IsFinite(frequency) ||
            frequency < VirtualInjectionProfile.MinimumFrequencyHz ||
            frequency > VirtualInjectionProfile.MaximumFrequencyHz)
        {
            SetVirtualInjectionInvalid($"Frequency must be {VirtualInjectionProfile.MinimumFrequencyHz:0}–{VirtualInjectionProfile.MaximumFrequencyHz:0} Hz before copying the table.");
            return;
        }

        var entries = new List<VirtualInjectionTableEntry>(_virtualInjectionRows.Count);
        foreach (var row in _virtualInjectionRows)
        {
            if (!row.TryCreateChannel(out var channel, out var error))
            {
                SetVirtualInjectionInvalid(error);
                return;
            }
            entries.Add(new VirtualInjectionTableEntry(
                row.Signal,
                channel.Enabled,
                channel.Rms,
                channel.AngleDegrees));
        }

        try
        {
            Clipboard.SetText(VirtualInjectionTableText.Serialize(
                new VirtualInjectionTableDocument(frequency, entries)));
            AddEvent("TABLE COPY", "4I+4V injection table copied to clipboard");
            StatusText.Text = "Virtual-injection table copied as tab-separated text for ARVREL or spreadsheet use.";
        }
        catch (ExternalException ex)
        {
            StatusText.Text = $"Clipboard is busy: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = $"Clipboard is unavailable: {ex.Message}";
        }
    }

    private void PasteVirtualInjectionTable()
    {
        string text;
        try
        {
            if (!Clipboard.ContainsText())
            {
                StatusText.Text = "Clipboard does not contain text to paste.";
                return;
            }
            text = Clipboard.GetText();
        }
        catch (ExternalException ex)
        {
            StatusText.Text = $"Clipboard is busy: {ex.Message}";
            return;
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = $"Clipboard is unavailable: {ex.Message}";
            return;
        }

        if (!VirtualInjectionTableText.TryParse(text, out var document, out var error) || document is null)
        {
            SetVirtualInjectionInvalid(error);
            return;
        }

        var entries = document.Entries.ToDictionary(entry => entry.Signal);
        _virtualInjectionApplyTimer?.Stop();
        _virtualInjectionEditorSync = true;
        try
        {
            foreach (var row in _virtualInjectionRows)
            {
                var entry = entries[row.Signal];
                row.IsEnabled = entry.Enabled;
                row.ValueText = entry.Rms.ToString("0.###", CultureInfo.InvariantCulture);
                row.AngleText = VirtualInjectionChannel.NormalizeAngle(entry.AngleDegrees)
                    .ToString("0.###", CultureInfo.InvariantCulture);
            }
            if (document.FrequencyHz is double frequency && _virtualInjectionFrequencyText is not null)
                _virtualInjectionFrequencyText.Text = frequency.ToString("0.###", CultureInfo.InvariantCulture);
            _pendingInjectionName = "Custom injection";
            if (_virtualInjectionPresetCombo is not null)
                _virtualInjectionPresetCombo.SelectedItem = null;
        }
        finally
        {
            _virtualInjectionEditorSync = false;
        }

        if (!TryApplyVirtualInjectionEditor())
            return;

        AddEvent("TABLE PASTE", "Validated 4I+4V injection table applied atomically");
        StatusText.Text = "Clipboard table validated and applied atomically. Running output follows the normal coherent-cycle rebuild.";
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
                return match;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }
}
