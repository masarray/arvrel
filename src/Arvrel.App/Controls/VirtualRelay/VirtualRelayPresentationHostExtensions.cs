using System.Collections.Generic;
using System.Windows;

namespace Arvrel.App.Controls.VirtualRelay;

/// <summary>
/// Keeps MainWindow's P6 host typed to the common WPF presentation base used by
/// both MainWindow and VirtualRelayControl, while forwarding relay-only chrome
/// operations only when the mounted host is the native virtual relay.
/// </summary>
internal static class VirtualRelayPresentationHostExtensions
{
    public static void SetSoftKeys(this FrameworkElement host, IReadOnlyList<VirtualRelaySoftKey> keys)
    {
        if (host is VirtualRelayControl relay)
            relay.SetSoftKeys(keys);
    }

    public static void SetLcdTrustState(this FrameworkElement host, string text, VirtualRelayLcdTone tone)
    {
        if (host is VirtualRelayControl relay)
            relay.SetLcdTrustState(text, tone);
    }

    public static void SetProgrammableLampLabels(this FrameworkElement host, IReadOnlyList<string> labels)
    {
        if (host is VirtualRelayControl relay)
            relay.SetProgrammableLampLabels(labels);
    }
}
