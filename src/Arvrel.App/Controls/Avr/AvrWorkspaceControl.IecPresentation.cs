using System.Windows;
using System.Windows.Controls;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private void RefreshIecControlPresentation()
    {
        if (!_hmiNavigationInitialized || _hmiPageContent is null)
            return;

        if (_hmiPage == HmiPage.System)
        {
            var status = _iec61850Server.GetStatus();
            SetHmiText(
                "IEC_STATS",
                $"Active {status.ActiveConnections} · Requests {status.ServedRequests} · Reports {status.ReportsSent} · CTRL {status.AcceptedControls}/{status.RejectedControls}");
            if (_hmiDynamicText.TryGetValue("IEC_ACTIVITY", out var activity))
                activity.Text = $"{status.LastActivity}\nCTRL: {status.LastControl}";
        }

        foreach (var text in FindVisualChildren<TextBlock>(_hmiPageContent))
        {
            if (text.Text.StartsWith("Remote commands are intentionally not mapped", StringComparison.OrdinalIgnoreCase))
            {
                text.Text = "IEC 61850 remote control is enabled in REMOTE authority. TapChg, Auto and LTCBlk use SBO/SBOw → Oper/Cancel with a 5 s selection timeout. Setpoint, bandwidth and T1 are writable setting points. LOCAL authority rejects SAS process writes.";
                text.TextWrapping = TextWrapping.Wrap;
            }
            else if (text.Text.StartsWith("SAS client: connect to this PC IPv4", StringComparison.OrdinalIgnoreCase))
            {
                text.Text = $"SAS client: connect to this PC IPv4 on TCP/102. Local IPv4: {LocalIpv4Summary()}. Services: association/browse/read/DataSet, BRCB/URCB + GI/integrity, SBO/SBOw/Oper/Cancel for ATCC1.Auto, ATCC1.LTCBlk and YLTC1.TapChg, plus REMOTE setting writes for BndCtr/BndWid/CtlDlTmms. All commands act only on the virtual AVR/OLTC.";
                text.TextWrapping = TextWrapping.Wrap;
            }
        }
    }
}
