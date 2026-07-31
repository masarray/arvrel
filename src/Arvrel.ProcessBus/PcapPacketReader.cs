using System.Buffers.Binary;

namespace Arvrel.ProcessBus;

public sealed record PcapPacket(DateTimeOffset Timestamp, ReadOnlyMemory<byte> Frame, int InterfaceId = 0);

public static class PcapPacketReader
{
    private const uint SectionHeaderBlock = 0x0A0D0D0A;
    private const uint InterfaceDescriptionBlock = 0x00000001;
    private const uint SimplePacketBlock = 0x00000003;
    private const uint EnhancedPacketBlock = 0x00000006;
    private const ushort EthernetLinkType = 1;

    private static readonly byte[] PcapMicrosecondsLittle = [0xD4, 0xC3, 0xB2, 0xA1];
    private static readonly byte[] PcapMicrosecondsBig = [0xA1, 0xB2, 0xC3, 0xD4];
    private static readonly byte[] PcapNanosecondsLittle = [0x4D, 0x3C, 0xB2, 0xA1];
    private static readonly byte[] PcapNanosecondsBig = [0xA1, 0xB2, 0x3C, 0x4D];

    public static IEnumerable<PcapPacket> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[4];
        ReadExactly(stream, signature);
        stream.Position = 0;

        var firstLittle = BinaryPrimitives.ReadUInt32LittleEndian(signature);
        return firstLittle == SectionHeaderBlock
            ? ReadPcapNg(stream).ToArray()
            : ReadClassicPcap(stream).ToArray();
    }

    private static IEnumerable<PcapPacket> ReadClassicPcap(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        var header = reader.ReadBytes(24);
        if (header.Length != 24)
            throw new InvalidDataException("Classic PCAP global header is truncated.");

        var magic = header.AsSpan(0, 4);
        var littleEndian = magic.SequenceEqual(PcapMicrosecondsLittle) ||
                           magic.SequenceEqual(PcapNanosecondsLittle);
        var bigEndian = magic.SequenceEqual(PcapMicrosecondsBig) ||
                        magic.SequenceEqual(PcapNanosecondsBig);
        if (!littleEndian && !bigEndian)
            throw new InvalidDataException("The file is neither classic PCAP nor PCAPNG.");

        var nanosecondResolution = magic.SequenceEqual(PcapNanosecondsLittle) ||
                                   magic.SequenceEqual(PcapNanosecondsBig);
        var linkType = ReadUInt32(header.AsSpan(20, 4), littleEndian);
        if (linkType != EthernetLinkType)
            throw new InvalidDataException($"Unsupported PCAP link type {linkType}; Ethernet link type 1 is required.");

        while (stream.Position < stream.Length)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining < 16)
                throw new InvalidDataException("Classic PCAP packet header is truncated.");

            var packetHeader = reader.ReadBytes(16);
            if (packetHeader.Length != 16)
                throw new InvalidDataException("Classic PCAP packet header is truncated.");

            var seconds = ReadUInt32(packetHeader.AsSpan(0, 4), littleEndian);
            var fraction = ReadUInt32(packetHeader.AsSpan(4, 4), littleEndian);
            var capturedLength = ReadUInt32(packetHeader.AsSpan(8, 4), littleEndian);
            ValidatePacketLength(capturedLength);

            var frame = reader.ReadBytes(checked((int)capturedLength));
            if (frame.Length != capturedLength)
            {
                throw new InvalidDataException(
                    $"Classic PCAP packet declares {capturedLength} byte(s), but only {frame.Length} were available.");
            }

            var ticks = nanosecondResolution ? fraction / 100 : fraction * 10;
            yield return new PcapPacket(DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(ticks), frame);
        }
    }

    private static IEnumerable<PcapPacket> ReadPcapNg(Stream stream)
    {
        var interfaces = new Dictionary<int, InterfaceClock>();
        var littleEndian = true;
        var haveSection = false;
        var lastTimestamp = DateTimeOffset.UnixEpoch;

        while (stream.Position < stream.Length)
        {
            var prefix = new byte[12];
            if (!TryReadExactly(stream, prefix))
                yield break;

            var rawType = BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(0, 4));
            if (rawType == SectionHeaderBlock)
            {
                var byteOrderBytes = prefix.AsSpan(8, 4);
                littleEndian = byteOrderBytes.SequenceEqual(new byte[] { 0x4D, 0x3C, 0x2B, 0x1A })
                    ? true
                    : byteOrderBytes.SequenceEqual(new byte[] { 0x1A, 0x2B, 0x3C, 0x4D })
                        ? false
                        : throw new InvalidDataException("Invalid PCAPNG byte-order magic.");
                var totalLength = ReadUInt32(prefix.AsSpan(4, 4), littleEndian);
                ValidateBlockLength(totalLength, 28);
                SkipRemainingBlock(stream, prefix, totalLength, littleEndian);
                interfaces.Clear();
                haveSection = true;
                continue;
            }

            if (!haveSection)
                throw new InvalidDataException("PCAPNG data appeared before a Section Header Block.");

            var blockType = ReadUInt32(prefix.AsSpan(0, 4), littleEndian);
            var blockLength = ReadUInt32(prefix.AsSpan(4, 4), littleEndian);
            ValidateBlockLength(blockLength, 12);
            var block = new byte[checked((int)blockLength)];
            prefix.CopyTo(block, 0);
            ReadExactly(stream, block.AsSpan(prefix.Length));
            ValidateTrailingLength(block, littleEndian);

            switch (blockType)
            {
                case InterfaceDescriptionBlock:
                {
                    if (blockLength < 20)
                        throw new InvalidDataException("PCAPNG Interface Description Block is truncated.");
                    var linkType = ReadUInt16(block.AsSpan(8, 2), littleEndian);
                    var clock = ParseInterfaceClock(block, littleEndian);
                    clock = clock with { IsEthernet = linkType == EthernetLinkType };
                    interfaces[interfaces.Count] = clock;
                    break;
                }
                case EnhancedPacketBlock:
                {
                    if (blockLength < 32)
                        throw new InvalidDataException("PCAPNG Enhanced Packet Block is truncated.");
                    var interfaceId = checked((int)ReadUInt32(block.AsSpan(8, 4), littleEndian));
                    if (!interfaces.TryGetValue(interfaceId, out var clock))
                        throw new InvalidDataException($"PCAPNG packet references unknown interface {interfaceId}.");
                    if (!clock.IsEthernet)
                        break;

                    var timestampHigh = ReadUInt32(block.AsSpan(12, 4), littleEndian);
                    var timestampLow = ReadUInt32(block.AsSpan(16, 4), littleEndian);
                    var capturedLength = ReadUInt32(block.AsSpan(20, 4), littleEndian);
                    ValidatePacketLength(capturedLength);
                    const int dataOffset = 28;
                    if (dataOffset + capturedLength > blockLength - 4)
                        throw new InvalidDataException("PCAPNG packet data is truncated.");

                    var timestampValue = ((ulong)timestampHigh << 32) | timestampLow;
                    lastTimestamp = clock.ToTimestamp(timestampValue);
                    yield return new PcapPacket(lastTimestamp, block.AsMemory(dataOffset, checked((int)capturedLength)).ToArray(), interfaceId);
                    break;
                }
                case SimplePacketBlock:
                {
                    if (blockLength < 16)
                        throw new InvalidDataException("PCAPNG Simple Packet Block is truncated.");
                    if (!interfaces.TryGetValue(0, out var clock) || !clock.IsEthernet)
                        break;
                    var originalLength = ReadUInt32(block.AsSpan(8, 4), littleEndian);
                    var available = checked((int)blockLength - 16);
                    var capturedLength = Math.Min(checked((int)originalLength), available);
                    if (capturedLength > 0)
                        yield return new PcapPacket(lastTimestamp, block.AsMemory(12, capturedLength).ToArray(), 0);
                    break;
                }
            }
        }
    }

    private static InterfaceClock ParseInterfaceClock(byte[] block, bool littleEndian)
    {
        var offset = 16;
        var end = block.Length - 4;
        var resolution = InterfaceClock.DefaultResolution;

        while (offset + 4 <= end)
        {
            var code = ReadUInt16(block.AsSpan(offset, 2), littleEndian);
            var length = ReadUInt16(block.AsSpan(offset + 2, 2), littleEndian);
            offset += 4;
            if (code == 0)
                break;
            if (offset + length > end)
                throw new InvalidDataException("PCAPNG interface option exceeds the block boundary.");
            if (code == 9 && length >= 1)
                resolution = InterfaceClock.DecodeResolution(block[offset]);
            offset += Align4(length);
        }

        return new InterfaceClock(false, resolution);
    }

    private static void SkipRemainingBlock(Stream stream, byte[] prefix, uint totalLength, bool littleEndian)
    {
        var remaining = checked((int)totalLength - prefix.Length);
        var tail = new byte[remaining];
        ReadExactly(stream, tail);
        var trailing = ReadUInt32(tail.AsSpan(tail.Length - 4, 4), littleEndian);
        if (trailing != totalLength)
            throw new InvalidDataException("PCAPNG block length trailer does not match the header.");
    }

    private static void ValidateTrailingLength(byte[] block, bool littleEndian)
    {
        var header = ReadUInt32(block.AsSpan(4, 4), littleEndian);
        var trailer = ReadUInt32(block.AsSpan(block.Length - 4, 4), littleEndian);
        if (header != trailer)
            throw new InvalidDataException("PCAPNG block length trailer does not match the header.");
    }

    private static void ValidatePacketLength(uint length)
    {
        if (length == 0 || length > 4_194_304)
            throw new InvalidDataException($"Invalid capture packet length: {length}.");
    }

    private static void ValidateBlockLength(uint length, uint minimum)
    {
        if (length < minimum || length % 4 != 0 || length > 16_777_216)
            throw new InvalidDataException($"Invalid PCAPNG block length: {length}.");
    }

    private static int Align4(int length) => (length + 3) & ~3;

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, bool littleEndian)
        => littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(source) : BinaryPrimitives.ReadUInt16BigEndian(source);

    private static uint ReadUInt32(ReadOnlySpan<byte> source, bool littleEndian)
        => littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(source) : BinaryPrimitives.ReadUInt32BigEndian(source);

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var readTotal = 0;
        while (readTotal < buffer.Length)
        {
            var read = stream.Read(buffer[readTotal..]);
            if (read == 0)
                throw new InvalidDataException("Capture file is truncated.");
            readTotal += read;
        }
    }

    private static bool TryReadExactly(Stream stream, Span<byte> buffer)
    {
        var readTotal = 0;
        while (readTotal < buffer.Length)
        {
            var read = stream.Read(buffer[readTotal..]);
            if (read == 0)
                return readTotal == 0;
            readTotal += read;
        }
        return true;
    }

    private sealed record InterfaceClock(bool IsEthernet, double TicksPerSecond)
    {
        public const double DefaultResolution = 1_000_000;

        public static double DecodeResolution(byte value)
        {
            var exponent = value & 0x7F;
            return (value & 0x80) == 0
                ? Math.Pow(10, exponent)
                : Math.Pow(2, exponent);
        }

        public DateTimeOffset ToTimestamp(ulong value)
        {
            var seconds = value / TicksPerSecond;
            var ticks = checked((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
            return DateTimeOffset.UnixEpoch.AddTicks(ticks);
        }
    }
}
