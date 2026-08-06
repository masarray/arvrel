using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class PcapPacketReaderTests
{
    [TestMethod]
    public void ReadsClassicPcapEthernetPacket()
    {
        var frame = BuildEthernetFrame();
        var path = Path.Combine(Path.GetTempPath(), $"arvrel-{Guid.NewGuid():N}.pcap");
        try
        {
            using (var stream = File.Create(path))
            {
                WriteUInt32(stream, 0xA1B2C3D4);
                WriteUInt16(stream, 2);
                WriteUInt16(stream, 4);
                WriteUInt32(stream, 0);
                WriteUInt32(stream, 0);
                WriteUInt32(stream, 65_535);
                WriteUInt32(stream, 1);

                WriteUInt32(stream, 1);
                WriteUInt32(stream, 250_000);
                WriteUInt32(stream, (uint)frame.Length);
                WriteUInt32(stream, (uint)frame.Length);
                stream.Write(frame);
            }

            var packets = PcapPacketReader.Read(path).ToArray();
            Assert.AreEqual(1, packets.Length);
            CollectionAssert.AreEqual(frame, packets[0].Frame.ToArray());
            Assert.AreEqual(DateTimeOffset.UnixEpoch.AddSeconds(1.25), packets[0].Timestamp);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadsPcapNgEnhancedPacket()
    {
        var frame = BuildEthernetFrame();
        var path = Path.Combine(Path.GetTempPath(), $"arvrel-{Guid.NewGuid():N}.pcapng");
        try
        {
            using (var stream = File.Create(path))
            {
                WriteSectionHeader(stream);
                WriteInterfaceDescription(stream);
                WriteEnhancedPacket(stream, frame, 1_500_000);
            }

            var packets = PcapPacketReader.Read(path).ToArray();
            Assert.AreEqual(1, packets.Length);
            CollectionAssert.AreEqual(frame, packets[0].Frame.ToArray());
            Assert.AreEqual(DateTimeOffset.UnixEpoch.AddSeconds(1.5), packets[0].Timestamp);
            Assert.AreEqual(0, packets[0].InterfaceId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void RejectsNonEthernetClassicPcap()
    {
        var path = Path.Combine(Path.GetTempPath(), $"arvrel-{Guid.NewGuid():N}.pcap");
        try
        {
            using (var stream = File.Create(path))
            {
                WriteUInt32(stream, 0xA1B2C3D4);
                WriteUInt16(stream, 2);
                WriteUInt16(stream, 4);
                WriteUInt32(stream, 0);
                WriteUInt32(stream, 0);
                WriteUInt32(stream, 65_535);
                WriteUInt32(stream, 101);
            }

            Assert.ThrowsExactly<InvalidDataException>(() => PcapPacketReader.Read(path).ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] BuildEthernetFrame()
    {
        var frame = new byte[60];
        frame[0] = 0x01;
        frame[1] = 0x0C;
        frame[2] = 0xCD;
        frame[3] = 0x04;
        frame[4] = 0x00;
        frame[5] = 0x00;
        frame[12] = 0x88;
        frame[13] = 0xBA;
        return frame;
    }

    private static void WriteSectionHeader(Stream stream)
    {
        WriteUInt32(stream, 0x0A0D0D0A);
        WriteUInt32(stream, 28);
        WriteUInt32(stream, 0x1A2B3C4D);
        WriteUInt16(stream, 1);
        WriteUInt16(stream, 0);
        WriteUInt64(stream, ulong.MaxValue);
        WriteUInt32(stream, 28);
    }

    private static void WriteInterfaceDescription(Stream stream)
    {
        WriteUInt32(stream, 1);
        WriteUInt32(stream, 20);
        WriteUInt16(stream, 1);
        WriteUInt16(stream, 0);
        WriteUInt32(stream, 65_535);
        WriteUInt32(stream, 20);
    }

    private static void WriteEnhancedPacket(Stream stream, byte[] frame, uint timestampMicroseconds)
    {
        var paddedLength = (frame.Length + 3) & ~3;
        var totalLength = checked((uint)(32 + paddedLength));
        WriteUInt32(stream, 6);
        WriteUInt32(stream, totalLength);
        WriteUInt32(stream, 0);
        WriteUInt32(stream, 0);
        WriteUInt32(stream, timestampMicroseconds);
        WriteUInt32(stream, (uint)frame.Length);
        WriteUInt32(stream, (uint)frame.Length);
        stream.Write(frame);
        if (paddedLength > frame.Length)
            stream.Write(new byte[paddedLength - frame.Length]);
        WriteUInt32(stream, totalLength);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt64(Stream stream, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        stream.Write(buffer);
    }
}
