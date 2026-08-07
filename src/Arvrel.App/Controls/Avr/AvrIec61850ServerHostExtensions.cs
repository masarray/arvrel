using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

internal static class AvrIec61850ServerHostExtensions
{
    public static void Publish(
        this AvrIec61850ServerHost host,
        AvrSnapshot snapshot,
        AvrSettings settings,
        double frequencyHz)
    {
        var remoteBlock = snapshot.Blocked &&
                          snapshot.Reason.StartsWith("Remote LTC blocking", StringComparison.OrdinalIgnoreCase);
        host.Publish(snapshot, settings, frequencyHz, remoteBlock);
    }
}
