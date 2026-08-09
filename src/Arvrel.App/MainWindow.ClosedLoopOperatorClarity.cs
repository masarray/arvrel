using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Arvrel.App;

public partial class MainWindow
{
    private const int ClosedLoopOperatorClarityMaximumAttempts = 24;
    private bool _closedLoopTimingPanelReflowed;
    private bool _closedLoopSetpointHeadersClarified;
    private int _closedLoopOperatorClarityAttempts;

    internal void InitializeClosedLoopOperatorClarity()
    {
        if (_closedLoopTimingPanelReflowed && _closedLoopSetpointHeadersClarified)
            return;

        if (!IsLoaded || _virtualInjectionView is null)
        {
            RetryClosedLoopOperatorClarity();
            return;
        }

        if (!_closedLoopTimingPanelReflowed)
            _closedLoopTimingPanelReflowed = TryReflowClosedLoopTimingPanel();
        if (!_closedLoopSetpointHeadersClarified)
            _closedLoopSetpointHeadersClarified = TryClarifyInjectionSetpointHeaders();

        if (!_closedLoopTimingPanelReflowed || !_closedLoopSetpointHeadersClarified)
            RetryClosedLoopOperatorClarity();
    }

    private void RetryClosedLoopOperatorClarity()
    {
        if (_closedLoopOperatorClarityAttempts++ >= ClosedLoopOperatorClarityMaximumAttempts)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(InitializeClosedLoopOperatorClarity));
    }

    private bool TryReflowClosedLoopTimingPanel()
    {
        if (_virtualInjectionView is null || _testSetTimingPanel is null)
            return false;

        var footer = _virtualInjectionView.Children
            .OfType<Grid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 2);
        if (footer is null)
            return false;

        // The timing result is primary test-equipment information, not a narrow
        // accessory beside CT controls. Give it one full row and keep source/CT
        // actions on the lower row.
        if (_virtualInjectionView.RowDefinitions.Count >= 3)
            _virtualInjectionView.RowDefinitions[2].Height = new GridLength(92);

        footer.RowDefinitions.Clear();
        footer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(51) });
        footer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

        foreach (UIElement child in footer.Children.Cast<UIElement>().ToArray())
        {
            Grid.SetRowSpan(child, 1);
            if (ReferenceEquals(child, _testSetTimingPanel))
                continue;

            Grid.SetRow(child, 1);
            if (child is FrameworkElement element)
                element.VerticalAlignment = VerticalAlignment.Center;
        }

        Grid.SetRow(_testSetTimingPanel, 0);
        Grid.SetColumn(_testSetTimingPanel, 0);
        Grid.SetColumnSpan(_testSetTimingPanel, Math.Max(1, footer.ColumnDefinitions.Count));
        _testSetTimingPanel.Margin = new Thickness(0, 0, 0, 5);
        _testSetTimingPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        _testSetTimingPanel.VerticalAlignment = VerticalAlignment.Stretch;

        return true;
    }

    private bool TryClarifyInjectionSetpointHeaders()
    {
        if (_virtualInjectionView is null)
            return false;

        var table = _virtualInjectionView.Children
            .OfType<DataGrid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 1);
        if (table is null || table.Columns.Count < 4)
            return false;

        table.Columns[2].Header = "RMS SET";
        table.Columns[3].Header = "ANGLE SET";
        table.ToolTip =
            "Configured source setpoints. After auto-stop these values are retained for repeatability; " +
            "the OUTPUT OFF / FROZEN CAPTURE banner is the authority for actual output state.";
        return true;
    }
}
