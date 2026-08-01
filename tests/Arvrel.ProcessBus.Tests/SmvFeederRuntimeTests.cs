#if ARIEC61850_SIBLING
using System.Buffers.Binary;
using AR.Iec61850.Ethernet;
using AR.Iec61850.SampledValues;
using AR.Iec61850.Scl;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class SmvFeederRuntimeTests
{
    [TestMethod]
    public void FourCurrentFourVoltageStreamFeedsDirectionalPhaseProtection()
    {
        var profile = CreateProfile();
        var source = MacAddress.Parse("02:00:00:00:00:67");
        var first = CreateFrame(profile, source, 0, 0);
        var settings = new ProtectionSettings
        {
            PhaseInstantaneousEnabled = false,
            PhaseTimeEnabled = false,
            EarthInstantaneousEnabled = false,
            EarthTimeEnabled = false,
            Feeder = new FeederProtectionSettings
            {
                DirectionalPhase67Enabled = true,
                DirectionalPhase67PickupA = 1.25,
                DirectionalPhase67Delay = TimeSpan.FromMilliseconds(60),
                DirectionalPhase67CharacteristicAngleDeg = 45,
                DirectionalPhase67MinimumPolarizingVoltageV = 5,
                DirectionalPhase67Sense = DirectionalSense.Forward
            }
        };
        var runtime = new SmvStreamRuntime("67P", first, settings, new SmvMeasurementContext());
        var start = DateTimeOffset.UtcNow;

        for (var sample = 0; sample < 400; sample++)
        {
            var frame = CreateFrame(profile, source, (ushort)sample, sample);
            runtime.Observe(start.AddTicks(sample * 2_500), frame, profile, replay: true);
        }

        var snapshot = runtime.Snapshot(start.AddMilliseconds(100), primaryDisplay: false, replay: true);
        Assert.IsNotNull(snapshot.Measurement.Phasors);
        Assert.AreEqual(63.5, snapshot.Measurement.Phasors.PhaseAVoltage.Magnitude, 0.2);
        Assert.IsTrue(snapshot.Protection.Feeder.PhaseDirectionalDecision.PolarizingAvailable);
        Assert.IsTrue(snapshot.Protection.Feeder.PhaseDirectionalDecision.InSelectedDirection,
            snapshot.Protection.Feeder.PhaseDirectionalDecision.Reason);
        Assert.IsTrue(snapshot.Protection.Feeder.DirectionalPhase67.Operated,
            snapshot.Protection.Feeder.DirectionalPhase67.Reason);
        Assert.IsTrue(snapshot.Protection.TripLatched);
        Assert.AreEqual(160, snapshot.Waveform.VoltageA.Count);
    }

    private static SampledValuesPublisherProfile CreateProfile()
    {
        var entries = new List<SclDataSetEntry>();
        var channels = new[]
        {
            "TCTR1/AmpSv.instMag.i", "TCTR2/AmpSv.instMag.i", "TCTR3/AmpSv.instMag.i", "TCTR4/AmpSv.instMag.i",
            "TVTR1/VolSv.instMag.i", "TVTR2/VolSv.instMag.i", "TVTR3/VolSv.instMag.i", "TVTR4/VolSv.instMag.i"
        };
        var index = 1;
        foreach (var channel in channels)
        {
            entries.Add(new SclDataSetEntry { Index = index++, SignalReference = channel, BType = "INT32" });
            entries.Add(new SclDataSetEntry { Index = index++, SignalReference = channel + ".q", BType = "Quality", IsQuality = true });
        }

        return SampledValuesPublisherProfile.Create(new SclSampledValuesStream
        {
            Kind = "SV",
            IedName = "MU67",
            LdInst = "MU",
            ControlName = "MSVCB01",
            ControlBlockReference = "MU67MU/LLN0.MSVCB01",
            DataSetName = "PhsMeas1",
            DataSetReference = "MU67MU/LLN0$PhsMeas1",
            ConfigurationRevision = 1,
            SvId = "MU67",
            SmvId = "MU67",
            SampleRate = 80,
            SampleMode = "SmpPerPeriod",
            NoAsdu = 1,
            Entries = entries,
            Address = new SclStreamAddress
            {
                AppId = 0x4067,
                AppIdText = "4067",
                DestinationMac = MacAddress.Parse("01:0C:CD:04:00:67"),
                DestinationMacText = "01-0C-CD-04-00-67"
            }
        });
    }

    private static SampledValuesFrame CreateFrame(
        SampledValuesPublisherProfile profile,
        MacAddress source,
        ushort sampleCount,
        int sampleIndex)
    {
        const int samplesPerCycle = 80;
        var angle = 2 * Math.PI * sampleIndex / samplesPerCycle;
        var currentShift = 45 * Math.PI / 180;
        var payload = new byte[64];

        WriteCurrent(payload, 0, 2 * Math.Sqrt(2) * Math.Sin(angle + currentShift));
        WriteCurrent(payload, 1, 2 * Math.Sqrt(2) * Math.Sin(angle - 2 * Math.PI / 3 + currentShift));
        WriteCurrent(payload, 2, 2 * Math.Sqrt(2) * Math.Sin(angle + 2 * Math.PI / 3 + currentShift));
        WriteCurrent(payload, 3, 0);
        WriteVoltage(payload, 4, 63.5 * Math.Sqrt(2) * Math.Sin(angle));
        WriteVoltage(payload, 5, 63.5 * Math.Sqrt(2) * Math.Sin(angle - 2 * Math.PI / 3));
        WriteVoltage(payload, 6, 63.5 * Math.Sqrt(2) * Math.Sin(angle + 2 * Math.PI / 3));
        WriteVoltage(payload, 7, 0);

        return profile.CreateFrame(source, sampleCount, payload, sampleSynchronization: 2, sampleCounterWrap: 4000);
    }

    private static void WriteCurrent(byte[] payload, int index, double engineeringValue)
        => WriteRaw(payload, index, checked((int)Math.Round(engineeringValue * 1000)));

    private static void WriteVoltage(byte[] payload, int index, double engineeringValue)
        => WriteRaw(payload, index, checked((int)Math.Round(engineeringValue * 100)));

    private static void WriteRaw(byte[] payload, int index, int raw)
    {
        var offset = index * 8;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, 4), raw);
    }
}
#endif
