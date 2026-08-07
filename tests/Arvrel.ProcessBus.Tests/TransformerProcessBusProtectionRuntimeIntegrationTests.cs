#if ARIEC61850_SIBLING
using System.Runtime.CompilerServices;
using AR.Iec61850.Ethernet;
using AR.Iec61850.Mms;
using AR.Iec61850.SampledValues;
using AR.Iec61850.Scl;
using Arvrel.Capture;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class TransformerProcessBusProtectionRuntimeIntegrationTests
{
    private const int SamplesPerCycle = 80;

    [TestMethod]
    public async Task ReplayAsync_DrivesTransformerRuntimeFrameByFrame()
    {
        var frames = BuildTransformerFrames();
        var replay = new RepeatingReplaySource(frames);
        var live = new BufferedLiveBackend(frames);
        await using var controller = new SmvProcessBusController(new ProtectionSettings(), live, replay);

        _ = await controller.ReplayAsync("discovery.pcap");
        var streams = controller.GetStreams();
        Assert.AreEqual(2, streams.Count);
        var hv = streams.Single(stream => stream.SvId == "HV-MU");
        var lv = streams.Single(stream => stream.SvId == "LV-MU");

        using var runtime = new TransformerProcessBusProtectionRuntime(
            controller,
            Configuration(hv.Key, lv.Key));
        var updateCount = 0;
        runtime.SnapshotChanged += (_, _) => updateCount++;

        var processed = await controller.ReplayAsync("runtime.pcap");
        var snapshot = runtime.CurrentSnapshot;

        Assert.AreEqual(frames.Count, processed);
        Assert.IsTrue(updateCount > 0);
        Assert.AreEqual(ProcessBusSourceMode.PcapReplay, snapshot.SourceMode);
        Assert.IsNotNull(snapshot.Measurement);
        Assert.IsNotNull(snapshot.Protection);
        Assert.AreEqual("PAIR_ALIGNED", snapshot.Pairing.Code);
        Assert.IsTrue(snapshot.Protection.Differential.Restrained87T.Operated);
        Assert.IsTrue(snapshot.Protection.Blocked);
        Assert.IsFalse(snapshot.TripRequested);
        Assert.IsFalse(snapshot.TripLatched);
    }

    [TestMethod]
    public async Task StartLiveAsync_DrivesSameRuntimeWhileCaptureIsRunning()
    {
        var frames = BuildTransformerFrames();
        var replay = new RepeatingReplaySource(frames);
        var live = new BufferedLiveBackend(frames);
        await using var controller = new SmvProcessBusController(new ProtectionSettings(), live, replay);

        _ = await controller.ReplayAsync("discovery.pcap");
        var streams = controller.GetStreams();
        var hv = streams.Single(stream => stream.SvId == "HV-MU");
        var lv = streams.Single(stream => stream.SvId == "LV-MU");

        using var runtime = new TransformerProcessBusProtectionRuntime(
            controller,
            Configuration(hv.Key, lv.Key));
        var updateCount = 0;
        runtime.SnapshotChanged += (_, _) => updateCount++;

        await controller.StartLiveAsync("test-adapter");
        await WaitUntilAsync(() => runtime.CurrentSnapshot.Measurement is not null);
        var snapshot = runtime.CurrentSnapshot;

        Assert.IsTrue(controller.IsRunning);
        Assert.IsTrue(updateCount > 0);
        Assert.AreEqual(ProcessBusSourceMode.LiveCapture, snapshot.SourceMode);
        Assert.IsNotNull(snapshot.Measurement);
        Assert.IsNotNull(snapshot.Protection);
        Assert.AreEqual("PAIR_ALIGNED", snapshot.Pairing.Code);

        await controller.StopAsync();
    }

    private static TransformerProtectionRuntimeConfiguration Configuration(string hvKey, string lvKey)
    {
        const double ratedPowerMva = 10;
        const double highVoltageKv = 100;
        const double lowVoltageKv = 10;
        return new TransformerProtectionRuntimeConfiguration(
            hvKey,
            lvKey,
            new TransformerNameplate(ratedPowerMva, highVoltageKv, lowVoltageKv, "Yyn0"),
            new TransformerWindingEngineering(new TransformerCtRatio(RatedCurrent(ratedPowerMva, highVoltageKv), 1)),
            new TransformerWindingEngineering(new TransformerCtRatio(RatedCurrent(ratedPowerMva, lowVoltageKv), 1)),
            new TransformerProtectionSettings
            {
                Differential87T = new TransformerDifferentialSettings
                {
                    Enabled = true,
                    MinimumPickupPu = 0.20,
                    Slope1 = 0.20,
                    Slope2 = 0.60,
                    SlopeBreakpointPu = 2,
                    OperateDelay = TimeSpan.Zero,
                    HarmonicSecurityMode = TransformerHarmonicSecurityMode.Blocking,
                    SecondHarmonicThreshold = 0.15,
                    FifthHarmonicThreshold = 0.35
                }
            });
    }

    private static IReadOnlyList<CapturedFrame> BuildTransformerFrames()
    {
        var highVoltageProfile = CreateProfile("HV-MU", 0x4000, "01:0C:CD:04:00:00");
        var lowVoltageProfile = CreateProfile("LV-MU", 0x4001, "01:0C:CD:04:00:01");
        var highVoltageSource = MacAddress.Parse("02:00:00:00:00:01");
        var lowVoltageSource = MacAddress.Parse("02:00:00:00:00:02");
        var start = DateTimeOffset.UtcNow;
        var frames = new List<CapturedFrame>(SamplesPerCycle * 4);

        for (ushort sample = 0; sample < SamplesPerCycle * 2; sample++)
        {
            var angle = 2 * Math.PI * sample / SamplesPerCycle;
            var ia = Math.Sqrt(2) * Math.Sin(angle);
            var hvPayload = BuildFixedPayload(highVoltageProfile, ia, 0, 0, 0);
            var lvPayload = BuildFixedPayload(lowVoltageProfile, ia, 0, 0, 0);
            var sampleTimestamp = start.AddTicks(sample * 2_500);

            frames.Add(new CapturedFrame(
                sampleTimestamp,
                highVoltageProfile.BuildEthernetFrame(
                    highVoltageSource,
                    sample,
                    samplePayloads: [hvPayload],
                    sampleSynchronization: 2)));
            frames.Add(new CapturedFrame(
                sampleTimestamp.AddTicks(500),
                lowVoltageProfile.BuildEthernetFrame(
                    lowVoltageSource,
                    sample,
                    samplePayloads: [lvPayload],
                    sampleSynchronization: 2)));
        }

        return frames;
    }

    private static SampledValuesPublisherProfile CreateProfile(string svId, ushort appId, string destination)
    {
        var entries = new List<SclDataSetEntry>();
        var channels = new[]
        {
            "TCTR1/AmpSv.instMag.i",
            "TCTR2/AmpSv.instMag.i",
            "TCTR3/AmpSv.instMag.i",
            "TCTR4/AmpSv.instMag.i",
            "TVTR1/VolSv.instMag.i",
            "TVTR2/VolSv.instMag.i",
            "TVTR3/VolSv.instMag.i",
            "TVTR4/VolSv.instMag.i"
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
            IedName = svId,
            LdInst = "MU",
            ControlName = "MSVCB01",
            ControlBlockReference = $"{svId}MU/LLN0.MSVCB01",
            DataSetName = "PhsMeas1",
            DataSetReference = $"{svId}MU/LLN0$PhsMeas1",
            ConfigurationRevision = 1,
            SvId = svId,
            SmvId = svId,
            SampleRate = 80,
            SampleMode = "SmpPerPeriod",
            NoAsdu = 1,
            Entries = entries,
            Address = new SclStreamAddress
            {
                AppId = appId,
                AppIdText = appId.ToString("X4", System.Globalization.CultureInfo.InvariantCulture),
                DestinationMac = MacAddress.Parse(destination),
                DestinationMacText = destination.Replace(':', '-')
            }
        });
    }

    private static byte[] BuildFixedPayload(
        SampledValuesPublisherProfile profile,
        double ia,
        double ib,
        double ic,
        double neutral)
    {
        var engineering = new[] { ia, ib, ic, neutral, 0.0, 0.0, 0.0, 0.0 };
        var values = new List<MmsDataValue>(16);
        for (var index = 0; index < engineering.Length; index++)
        {
            var scale = index >= 4 ? 100.0 : 1000.0;
            values.Add(MmsDataValue.Integer(checked((int)Math.Round(engineering[index] * scale))));
            values.Add(MmsDataValue.BitString(0, SampledValueQuality.Good.ToBytes()));
        }
        return profile.BuildPayload(values);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }
        Assert.Fail("Timed out waiting for the live transformer runtime to receive an aligned pair.");
    }

    private static double RatedCurrent(double ratedPowerMva, double ratedVoltageKv)
        => ratedPowerMva * 1_000_000 / (Math.Sqrt(3) * ratedVoltageKv * 1_000);

    private sealed class RepeatingReplaySource : ICaptureReplaySource
    {
        private readonly IReadOnlyList<CapturedFrame> _frames;

        public RepeatingReplaySource(IReadOnlyList<CapturedFrame> frames)
        {
            _frames = frames;
        }

        public string SourceId => "transformer-test-replay";
        public bool CanRead(string path) => true;

        public async IAsyncEnumerable<CapturedFrame> ReadAsync(
            string path,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            foreach (var frame in _frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return frame;
            }
        }
    }

    private sealed class BufferedLiveBackend : ILiveCaptureBackend
    {
        private readonly IReadOnlyList<CapturedFrame> _frames;

        public BufferedLiveBackend(IReadOnlyList<CapturedFrame> frames)
        {
            _frames = frames;
        }

        public string BackendId => "transformer-test-live";
        public string DisplayName => "Transformer test live capture";
        public bool IsAvailable => true;
        public string AvailabilityMessage => "Transformer test backend ready.";

        public IReadOnlyList<CaptureAdapter> ListAdapters()
            => [new CaptureAdapter("test-adapter", "Test adapter", "02-00-00-00-00-FF", BackendId)];

        public async IAsyncEnumerable<CapturedFrame> CaptureAsync(
            string adapterId,
            CaptureOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            foreach (var frame in _frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return frame;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
#endif
