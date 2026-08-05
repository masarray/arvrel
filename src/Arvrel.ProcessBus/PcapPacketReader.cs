using Arvrel.Capture;

namespace Arvrel.ProcessBus;

/// <summary>
/// Compatibility record retained for existing process-bus callers.
/// New code should consume <see cref="CapturedFrame"/> through
/// <see cref="ICaptureReplaySource"/>.
/// </summary>
public sealed record PcapPacket(
    DateTimeOffset Timestamp,
    ReadOnlyMemory<byte> Frame,
    int InterfaceId = 0);

/// <summary>
/// Compatibility facade. PCAP parsing now belongs to the portable
/// <c>Arvrel.Capture</c> project.
/// </summary>
public static class PcapPacketReader
{
    public static IEnumerable<PcapPacket> Read(string path)
        => PcapFileReader.Read(path)
            .Select(packet => new PcapPacket(
                packet.Timestamp,
                packet.Frame,
                packet.InterfaceId));
}
