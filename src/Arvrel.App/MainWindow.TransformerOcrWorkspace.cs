using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.App.Controls.VirtualRelay;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private Border? _transformerSharedRelayHost;
    private VirtualRelayControl? _ocrWorkspaceRelay;
    private bool _transformerOcrWorkspaceHooked;
    private bool _transformerOcrWorkspaceMounted;

    /// <summary>
    /// Mounts the Transformer faceplate into the exact relay host already used by
    /// the OCR workspace. The waveform, injection, phasor, source toolbar and lower
    /// operation workspace are not recreated or copied.
    /// </summary>
    private bool MountTransformerFaceplateIntoOcrWorkspace()
    {
        if (_transformerFaceplate is null || _protectionWorkspace is null)
            return false;

        if (!_transformerOcrWorkspaceHooked)
        {
            // MainWindow.Timer_Tick is registered in the constructor. Registering this
            // projection afterwards means OCR waveform/phasor rendering runs first and
            // the Transformer operation projection is the final presentation step.
            _timer.Tick += TransformerOcrWorkspace_Tick;
            _transformerOcrWorkspaceHooked = true;
        }

        if (_ocrWorkspaceRelay is null || _transformerSharedRelayHost is null)
        {
            _ocrWorkspaceRelay = MultiIedVisualDescendants<VirtualRelayControl>(_protectionWorkspace)
                .FirstOrDefault(relay => !ReferenceEquals(relay, _transformerFaceplate));
            _transformerSharedRelayHost = _ocrWorkspaceRelay is null
                ? null
                : VisualTreeHelper.GetParent(_ocrWorkspaceRelay) as Border;
        }

        if (_ocrWorkspaceRelay is null || _transformerSharedRelayHost is null)
        {
            StatusText.Text = "Transformer IED could not resolve the shared OCR relay host.";
            AddEvent("87T UI ERROR", "Shared OCR relay host not found");
            return false;
        }

        // The P17 compatibility landing is retained only as a lifecycle owner for the
        // Transformer relay instance. It must never become the operator workspace.
        if (_transformerLanding?.Child == _transformerFaceplate)
            _transformerLanding.Child = null;

        if (!ReferenceEquals(_transformerSharedRelayHost.Child, _transformerFaceplate))
            _transformerSharedRelayHost.Child = _transformerFaceplate;

        _transformerOcrWorkspaceMounted = true;
        _protectionWorkspace.Visibility = Visibility.Visible;
        if (_transformerLanding is not null)
            _transformerLanding.Visibility = Visibility.Collapsed;
        OperatingModeCombo.Visibility = Visibility.Visible;

        RenderTransformerOperationWorkspace();
        return true;
    }

    private void RestoreOcrRelayIntoSharedWorkspace()
    {
        if (!_transformerOcrWorkspaceMounted)
            return;

        if (_transformerSharedRelayHost is not null && _ocrWorkspaceRelay is not null)
            _transformerSharedRelayHost.Child = _ocrWorkspaceRelay;

        _transformerOcrWorkspaceMounted = false;

        // Restore the OCR setting captions and current evidence immediately instead
        // of waiting for a later settings change or timer cycle.
        UpdateSettingSummaries();
        if (SourceCombo.SelectedIndex == 0)
            RenderInitialFrame();
        else
            RenderSelectedProcessBusStream();
        RefreshRelayAnnunciation();
    }

    private void TransformerOcrWorkspace_Tick(object? sender, EventArgs e)
    {
        if (_transformerOcrWorkspaceMounted)
            RenderTransformerOperationWorkspace();
    }

    /// <summary>
    /// Projects authoritative Transformer runtime state into the four compact
    /// operation channels already present in the OCR workspace. This is display-only:
    /// no differential, REF, harmonic or CT-security decision is recalculated here.
    /// </summary>
    private void RenderTransformerOperationWorkspace()
    {
        if (!_transformerOcrWorkspaceMounted)
            return;

        var snapshot = _transformerLastSnapshot;
        var settings = snapshot?.EffectiveSettings;
        var protection = snapshot?.Protection;

        if (settings is null)
        {
            Phase50SettingText.Text = "87T · configure in F4";
            Phase51SettingText.Text = "87T-HS · configure in F4";
            Earth50SettingText.Text = "REF HV · configure in F4";
            Earth51SettingText.Text = "REF LV · configure in F4";
        }
        else
        {
            var differential = settings.Differential87T;
            Phase50SettingText.Text = $"87T · Is1 {differential.Is1Pu:0.##} pu / {differential.OperateDelay.TotalMilliseconds:0} ms";
            Phase51SettingText.Text = $"87T-HS · {differential.HighSetPickupPu:0.##} pu / {differential.HighSetDelay.TotalMilliseconds:0} ms";
            Earth50SettingText.Text = $"REF HV · {settings.RefHighVoltage.MinimumPickupPu:0.##} pu / {settings.RefHighVoltage.OperateDelay.TotalMilliseconds:0} ms";
            Earth51SettingText.Text = $"REF LV · {settings.RefLowVoltage.MinimumPickupPu:0.##} pu / {settings.RefLowVoltage.OperateDelay.TotalMilliseconds:0} ms";
        }

        ProjectTransformerStage(
            Phase50StateText,
            Phase50Progress,
            protection?.Differential.Restrained87T);
        ProjectTransformerStage(
            Phase51StateText,
            Phase51Progress,
            protection?.Differential.HighSet87T);
        ProjectTransformerStage(
            Earth50StateText,
            Earth50Progress,
            protection?.RefHighVoltage.Element);
        ProjectTransformerStage(
            Earth51StateText,
            Earth51Progress,
            protection?.RefLowVoltage.Element);

        if (snapshot is null)
        {
            ProtectionReasonText.Text = "  ·  Waiting for paired HV/LV Transformer runtime · F4 Engineering";
            PermissionText.Text = "PAIR REQUIRED";
            PermissionText.Foreground = WarningBrush;
            EventTraceText.Text = "87T         Shared OCR waveform/injection workspace\nPAIR        Bind two distinct HV/LV SV streams in F4\nRUNTIME     Waiting for authoritative Transformer snapshot";
            return;
        }

        ProtectionReasonText.Text = $"  ·  {TrimTransformerWorkspaceLine(snapshot.DecisionReason, 78)}";
        switch (snapshot.State)
        {
            case TransformerRuntimeState.Ready:
            case TransformerRuntimeState.Pickup:
                PermissionText.Text = "TRIP PERMITTED";
                PermissionText.Foreground = HealthyBrush;
                break;
            case TransformerRuntimeState.TripLatched:
                PermissionText.Text = "TRIP LATCHED";
                PermissionText.Foreground = TripBrush;
                break;
            case TransformerRuntimeState.PairBlocked:
                PermissionText.Text = "PAIR BLOCKED";
                PermissionText.Foreground = WarningBrush;
                break;
            case TransformerRuntimeState.ProtectionBlocked:
                PermissionText.Text = "PROTECTION BLOCKED";
                PermissionText.Foreground = WarningBrush;
                break;
            default:
                PermissionText.Text = "WAITING FOR PAIR";
                PermissionText.Foreground = WarningBrush;
                break;
        }

        var active = string.IsNullOrWhiteSpace(protection?.ActiveElement) ? "none" : protection.ActiveElement;
        EventTraceText.Text =
            $"STATE       {snapshot.State.ToString().ToUpperInvariant()}\n" +
            $"ACTIVE      {TrimTransformerWorkspaceLine(active, 24)}\n" +
            $"HV/LV       {TrimTransformerWorkspaceLine(snapshot.HighVoltageStreamKey, 12)} / {TrimTransformerWorkspaceLine(snapshot.LowVoltageStreamKey, 12)}\n" +
            $"DECISION    {TrimTransformerWorkspaceLine(snapshot.DecisionReason, 34)}";
    }

    private static void ProjectTransformerStage(
        TextBlock stateText,
        ProgressBar progress,
        TransformerElementSnapshot? stage)
    {
        if (stage is null)
        {
            stateText.Text = "WAITING";
            stateText.Foreground = WarningBrush;
            progress.Value = 0;
            return;
        }

        stateText.Text = stage.Operated
            ? "OPERATED"
            : stage.Pickup
                ? "PICKUP"
                : stage.State.ToString().ToUpperInvariant();
        stateText.Foreground = stage.Operated
            ? TripBrush
            : stage.Pickup
                ? WarningBrush
                : HealthyBrush;
        progress.Value = Math.Clamp(stage.Progress * 100, 0, 100);
    }

    private static string TrimTransformerWorkspaceLine(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";
        var singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= maxLength ? singleLine : singleLine[..Math.Max(1, maxLength - 1)] + "…";
    }
}
