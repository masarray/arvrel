using Arvrel.Application.Laboratory;
using Arvrel.Desktop.ViewModels;
using Arvrel.ProcessBus;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class DesktopPresentationSnapshotTests
{
    [TestMethod]
    public void InternalProjection_PreservesMeasurementProtectionAndWaveformAuthority()
    {
        var session = new InternalLabSession(new ProtectionSettings());
        var tick = session.CaptureFrame();

        var projection = DesktopPresentationSnapshot.FromInternal(
            tick,
            session.Scenario.SampleCounter,
            session.Scenario.ActiveProfile.Name,
            session.Scenario.OutputState);

        Assert.AreEqual("INTERNAL VIRTUAL TEST SET", projection.SourceMode);
        Assert.AreEqual(session.Scenario.ActiveProfile.Name, projection.SourceIdentity);
        Assert.AreSame(tick.Scenario.Measurement, projection.Measurement);
        Assert.AreSame(tick.Protection, projection.Protection);
        Assert.AreSame(tick.Scenario.Waveform, projection.Waveform);
    }

    [TestMethod]
    public void ReplayProjection_ConvertsRuntimeSnapshotWithoutChangingProtectionState()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var protection = ProtectionSnapshot.Ready(timestamp);
        var measurement = new MeasurementFrame(
            timestamp,
            8.4,
            1.0,
            1.0,
            7.4,
            new SmvTrustState(true, true, true, "GOOD", "Trusted replay window"));
        var runtime = SmvRuntimeSnapshot.Empty with
        {
            Stream = new SmvStreamInfo("stream-1", 0x4000, "MU01", "01-0C-CD-04-00-01", 100, "GOOD", true),
            Timestamp = timestamp,
            Measurement = measurement,
            Protection = protection,
            Waveform = new SmvWaveformWindow(
                new[] { 1.0, 2.0 },
                new[] { 3.0, 4.0 },
                new[] { 5.0, 6.0 },
                new[] { 7.0, 8.0 },
                50,
                80),
            SampleCounter = 123,
            SourceSummary = "PCAP replay",
            TrustSummary = "GOOD · trusted replay window",
            MappingSummary = "SCL bound",
            Diagnostics = new[] { "Replay frame accepted" }
        };

        var projection = DesktopPresentationSnapshot.FromProcessBus(runtime, replay: true);

        Assert.AreEqual("PCAP / PCAPNG REPLAY", projection.SourceMode);
        Assert.AreEqual(runtime.Stream.DisplayName, projection.SourceIdentity);
        Assert.AreSame(measurement, projection.Measurement);
        Assert.AreSame(protection, projection.Protection);
        Assert.AreEqual(123L, projection.SampleCounter);
        Assert.AreEqual(50d, projection.Waveform.FrequencyHz);
        Assert.AreEqual(80, projection.Waveform.SamplesPerCycle);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0 }, projection.Waveform.PhaseA);
        CollectionAssert.AreEqual(new[] { 7.0, 8.0 }, projection.Waveform.Residual);
        Assert.AreEqual("SCL bound", projection.MappingSummary);
        Assert.AreEqual("Replay frame accepted", projection.Diagnostics.Single());
    }
}
