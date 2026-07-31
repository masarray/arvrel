#if ARIEC61850_SIBLING
using System.Buffers.Binary;
using AR.Iec61850.Ethernet;
using AR.Iec61850.SampledValues;
using AR.Iec61850.Scl;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class SmvRuntimeTests
{
    [TestMethod]
    public void BuildsTwoCycleWindowAndPermitsTripForHealthyBoundStream()
    {
        var profile = CreateProfile();
        var source = MacAddress.Parse("02:00:00:00:00:01");
        var first = CreateFrame(profile, source, 0, 1, 1.02, 0.98, 0);
        var runtime = new SmvStreamRuntime("test", first, new ProtectionSettings(), new SmvMeasurementContext());
        var start = DateTimeOffset.UtcNow;

        for (ushort sample = 0; sample < 160; sample++)
        {
            var angle = sample / 80.0 * Math.PI * 2;
            var frame = CreateFrame(
                profile,
                source,
                sample,
                Math.Sqrt(2) * Math.Sin(angle),
                1.02 * Math.Sqrt(2) * Math.Sin(angle - 2 * Math.PI / 3),
                0.98 * Math.Sqrt(2) * Math.Sin(angle + 2 * Math.PI / 3),
                0);
            runtime.Observe(start.AddTicks(sample * 2_500), frame, profile, replay: true);
        }

        var snapshot = runtime.Snapshot(start.AddMilliseconds(40), primaryDisplay: false, replay: true);
        Assert.IsTrue(snapshot.Measurement.SmvTrust.AllowsTrip, snapshot.TrustSummary);
        Assert.AreEqual(1, snapshot.Measurement.PhaseA, 0.03);
        Assert.AreEqual(1.02, snapshot.Measurement.PhaseB, 0.03);
        Assert.AreEqual(0.98, snapshot.Measurement.PhaseC, 0.03);
        Assert.AreEqual(160, snapshot.Waveform.PhaseA.Count);
        Assert.AreEqual(0, snapshot.SequenceGapCount);
        Assert.IsTrue(snapshot.Stream.IsSclBound);
    }

    [TestMethod]
    public void TripsOnSustainedPhaseAndEarthFaultFromSmvSamples()
    {
        var profile = CreateProfile();
        var source = MacAddress.Parse("02:00:00:00:00:01");
        var first = CreateFrame(profile, source, 0, 8.4, 1, 1, 7.3);
        var runtime = new SmvStreamRuntime("fault", first, new ProtectionSettings(), new SmvMeasurementContext());
        var start = DateTimeOffset.UtcNow;

        for (ushort sample = 0; sample < 480; sample++)
        {
            var angle = sample / 80.0 * Math.PI * 2;
            var frame = CreateFrame(
                profile,
                source,
                sample,
                8.4 * Math.Sqrt(2) * Math.Sin(angle),
                Math.Sqrt(2) * Math.Sin(angle - 2 * Math.PI / 3),
                Math.Sqrt(2) * Math.Sin(angle + 2 * Math.PI / 3),
                7.3 * Math.Sqrt(2) * Math.Sin(angle));
            runtime.Observe(start.AddTicks(sample * 2_500), frame, profile, replay: true);
        }

        var snapshot = runtime.Snapshot(start.AddMilliseconds(120), primaryDisplay: false, replay: true);
        Assert.IsTrue(snapshot.Protection.TripLatched, snapshot.Protection.DecisionReason);
        Assert.IsFalse(snapshot.Protection.Blocked);
        Assert.IsTrue(snapshot.Measurement.SmvTrust.AllowsTrip);
        Assert.IsTrue(snapshot.Protection.Phase50.Operated || snapshot.Protection.Earth50.Operated);
    }

    [TestMethod]
    public void BlocksTripAfterSampleCounterGap()
    {
        var profile = CreateProfile();
        var source = MacAddress.Parse("02:00:00:00:00:01");
        var first = CreateFrame(profile, source, 0, 8.4, 1, 1, 7.3);
        var runtime = new SmvStreamRuntime("gap", first, new ProtectionSettings(), new SmvMeasurementContext());
        var start = DateTimeOffset.UtcNow;

        for (var index = 0; index < 180; index++)
        {
            var sample = (ushort)(index == 120 ? index + 1 : index);
            var angle = index / 80.0 * Math.PI * 2;
            var frame = CreateFrame(
                profile,
                source,
                sample,
                8.4 * Math.Sqrt(2) * Math.Sin(angle),
                Math.Sqrt(2) * Math.Sin(angle - 2 * Math.PI / 3),
                Math.Sqrt(2) * Math.Sin(angle + 2 * Math.PI / 3),
                7.3 * Math.Sqrt(2) * Math.Sin(angle));
            runtime.Observe(start.AddTicks(index * 2_500), frame, profile, replay: true);
        }

        var snapshot = runtime.Snapshot(start.AddMilliseconds(45), primaryDisplay: false, replay: true);
        Assert.IsFalse(snapshot.Measurement.SmvTrust.AllowsTrip);
        Assert.AreEqual("SMPCNT_DISCONTINUITY", snapshot.Measurement.SmvTrust.Code);
        Assert.IsTrue(snapshot.SequenceGapCount > 0 || snapshot.OutOfOrderCount > 0);
    }

    private static SampledValuesPublisherProfile CreateProfile()
    {
        var entries = new List<SclDataSetEntry>();
        var channels = new[] { "TCTR1/AmpSv.instMag.i", "TCTR2/AmpSv.instMag.i", "TCTR3/AmpSv.instMag.i", "TCTR4/AmpSv.instMag.i", "TVTR1/VolSv.instMag.i", "TVTR2/VolSv.instMag.i", "TVTR3/VolSv.instMag.i", "TVTR4/VolSv.instMag.i" };
        var index = 1;
        foreach (var channel in channels)
        {
            entries.Add(new SclDataSetEntry { Index = index++, SignalReference = channel, BType = "INT32" });
            entries.Add(new SclDataSetEntry { Index = index++, SignalReference = channel + ".q", BType = "Quality", IsQuality = true });
        }

        return SampledValuesPublisherProfile.Create(new SclSampledValuesStream
        {
            Kind = "SV",
            IedName = "MU01",
            LdInst = "MU",
            ControlName = "MSVCB01",
            ControlBlockReference = "MU01MU/LLN0.MSVCB01",
            DataSetName = "PhsMeas1",
            DataSetReference = "MU01MU/LLN0$PhsMeas1",
            ConfigurationRevision = 1,
            SvId = "MU01",
            SmvId = "MU01",
            SampleRate = 80,
            SampleMode = "SmpPerPeriod",
            NoAsdu = 1,
            Entries = entries,
            Address = new SclStreamAddress
            {
                AppId = 0x4000,
                AppIdText = "4000",
                DestinationMac = MacAddress.Parse("01:0C:CD:04:00:00"),
                DestinationMacText = "01-0C-CD-04-00-00"
            }
        });
    }

    private static SampledValuesFrame CreateFrame(
        SampledValuesPublisherProfile profile,
        MacAddress source,
        ushort sampleCount,
        double ia,
        double ib,
        double ic,
        double neutral)
    {
        var payload = new byte[64];
        WritePair(payload, 0, ia);
        WritePair(payload, 1, ib);
        WritePair(payload, 2, ic);
        WritePair(payload, 3, neutral);
        return profile.CreateFrame(source, sampleCount, payload, sampleSynchronization: 2, sampleCounterWrap: 4000);
    }

    private static void WritePair(byte[] payload, int index, double engineeringValue)
    {
        var offset = index * 8;
        var raw = checked((int)Math.Round(engineeringValue * 1000));
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, 4), raw);
    }
}
#endif
