using System.Reflection;
using Arvrel.Desktop.ViewModels;
using Arvrel.ProcessBus;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class DisplayHandoverTests
{
    [TestMethod]
    public async Task ProcessBusDisplayRequiresASelectedCoherentTwoCycleWindow()
    {
        await using var viewModel = new MainWindowViewModel();

        viewModel.ActivateProcessBusDisplayCommand.Execute(null);

        Assert.IsTrue(viewModel.IsInternalDisplayActive);
        Assert.IsFalse(viewModel.IsProcessBusDisplayActive);
        StringAssert.Contains(viewModel.DisplayHandoverStatus, "Select an SV stream");

        var stream = CreateStream("stream-a", "TEST_A");
        SelectStreamForTest(viewModel, stream);
        viewModel.ActivateProcessBusDisplayCommand.Execute(null);

        Assert.IsTrue(viewModel.IsInternalDisplayActive);
        StringAssert.Contains(viewModel.DisplayHandoverStatus, "coherent two-cycle");
    }

    [TestMethod]
    public async Task CoherentSnapshotCanTakeDisplayWithoutResettingInternalLabState()
    {
        await using var viewModel = new MainWindowViewModel();
        viewModel.InjectAgFault();
        for (var index = 0; index < 6; index++)
            viewModel.Tick();

        Assert.IsTrue(viewModel.TripLatched);
        var internalPhaseA = viewModel.PhaseAText;
        var internalWaveformCount = viewModel.Waveform.PhaseA.Length;

        var stream = CreateStream("stream-a", "TEST_A");
        var coherent = CreateSnapshot(stream, 8.5, SmvTrustState.Healthy, samplesPerCycle: 80, sampleCount: 160);
        ProjectSnapshotForTest(viewModel, coherent);
        viewModel.ActivateProcessBusDisplayCommand.Execute(null);

        Assert.IsTrue(viewModel.IsProcessBusDisplayActive);
        Assert.AreEqual("8.500 A", viewModel.PhaseAText);
        Assert.AreEqual(160, viewModel.Waveform.PhaseA.Length);
        StringAssert.Contains(viewModel.ActiveDisplaySourceText, "PROCESS BUS");
        StringAssert.Contains(viewModel.SourceModeText, "PROCESS BUS");

        viewModel.ActivateInternalDisplayCommand.Execute(null);

        Assert.IsTrue(viewModel.IsInternalDisplayActive);
        Assert.AreEqual(internalPhaseA, viewModel.PhaseAText);
        Assert.AreEqual(internalWaveformCount, viewModel.Waveform.PhaseA.Length);
        Assert.IsTrue(viewModel.TripLatched, "Internal trip latch must survive display handover.");
    }

    [TestMethod]
    public async Task StaleSnapshotUpdatesTrustButRetainsLastCoherentWaveform()
    {
        await using var viewModel = new MainWindowViewModel();
        var stream = CreateStream("stream-a", "TEST_A");
        var coherent = CreateSnapshot(stream, 6.25, SmvTrustState.Healthy, 80, 160);
        ProjectSnapshotForTest(viewModel, coherent);
        viewModel.ActivateProcessBusDisplayCommand.Execute(null);

        var staleTrust = new SmvTrustState(
            AllowsMeasurement: false,
            AllowsPickup: false,
            AllowsTrip: false,
            Code: "STALE",
            Detail: "No fresh Sampled Values frame.");
        var stale = CreateSnapshot(stream, 99, staleTrust, 80, 24);
        ProjectRawSnapshotForTest(viewModel, stale);

        Assert.IsTrue(viewModel.IsProcessBusDisplayActive);
        Assert.AreEqual("6.250 A", viewModel.PhaseAText, "Last coherent measurement must remain visible.");
        Assert.AreEqual(160, viewModel.Waveform.PhaseA.Length, "Incomplete waveform must not replace the accepted window.");
        Assert.IsFalse(viewModel.AllowsTrip);
        StringAssert.Contains(viewModel.TrustStateText, "STALE");
    }

    [TestMethod]
    public async Task ChangingSelectedStreamReturnsDisplayToInternalUntilNewWindowIsCoherent()
    {
        await using var viewModel = new MainWindowViewModel();
        var first = CreateStream("stream-a", "TEST_A");
        ProjectSnapshotForTest(viewModel, CreateSnapshot(first, 4.5, SmvTrustState.Healthy, 80, 160));
        viewModel.ActivateProcessBusDisplayCommand.Execute(null);
        Assert.IsTrue(viewModel.IsProcessBusDisplayActive);

        InvokePrivate(viewModel, "PrepareProcessBusDisplayStream", "stream-b");

        Assert.IsTrue(viewModel.IsInternalDisplayActive);
        StringAssert.Contains(viewModel.DisplayHandoverStatus, "returned to INTERNAL LAB");
        StringAssert.Contains(viewModel.ProcessBusDisplayReadinessText, "Waiting");
    }

    private static SmvStreamInfo CreateStream(string key, string svId)
        => new(key, 0x4000, svId, "01-0C-CD-04-00-01", null, "HEALTHY", true);

    private static SmvRuntimeSnapshot CreateSnapshot(
        SmvStreamInfo stream,
        double phaseA,
        SmvTrustState trust,
        int samplesPerCycle,
        int sampleCount)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var phaseAWindow = Enumerable.Repeat(phaseA, sampleCount).ToArray();
        var phaseBWindow = Enumerable.Repeat(2.0, sampleCount).ToArray();
        var phaseCWindow = Enumerable.Repeat(3.0, sampleCount).ToArray();
        var residualWindow = Enumerable.Repeat(0.1, sampleCount).ToArray();
        var protection = ProtectionSnapshot.Ready(timestamp) with { SmvTrust = trust };

        return new SmvRuntimeSnapshot(
            stream,
            timestamp,
            new MeasurementFrame(timestamp, phaseA, 2, 3, 0.1, trust),
            protection,
            new SmvWaveformWindow(
                phaseAWindow,
                phaseBWindow,
                phaseCWindow,
                residualWindow,
                50,
                samplesPerCycle),
            SampleCounter: 240,
            SampleSynchronization: 2,
            SamplesPerCycle: samplesPerCycle,
            FramesPerSecond: 4000,
            FrameCount: 1000,
            AsduCount: 1000,
            SequenceGapCount: trust.Code == "STALE" ? 1 : 0,
            DuplicateCount: 0,
            OutOfOrderCount: 0,
            InvalidQualityCount: 0,
            ScalingResolved: true,
            ScalingSummary: "SCL scaling resolved",
            MappingSummary: "4I mapped",
            TrustSummary: trust.Detail,
            SclSummary: "TEST SCL",
            SourceSummary: "TEST SOURCE",
            Diagnostics: Array.Empty<string>());
    }

    private static void SelectStreamForTest(MainWindowViewModel viewModel, SmvStreamInfo stream)
    {
        SetPrivateField(viewModel, "_selectedProcessBusStream", stream);
        InvokePrivate(viewModel, "PrepareProcessBusDisplayStream", stream.Key);
    }

    private static void ProjectSnapshotForTest(MainWindowViewModel viewModel, SmvRuntimeSnapshot snapshot)
    {
        SelectStreamForTest(viewModel, snapshot.Stream);
        ProjectRawSnapshotForTest(viewModel, snapshot);
    }

    private static void ProjectRawSnapshotForTest(MainWindowViewModel viewModel, SmvRuntimeSnapshot snapshot)
    {
        SetPrivateField(viewModel, "_processBusSnapshot", snapshot);
        InvokePrivate(viewModel, "ObserveProcessBusDisplaySnapshot", snapshot);
    }

    private static void SetPrivateField<T>(MainWindowViewModel viewModel, string name, T value)
    {
        var field = typeof(MainWindowViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException($"Private field '{name}' was not found.");
        field.SetValue(viewModel, value);
    }

    private static void InvokePrivate(MainWindowViewModel viewModel, string name, params object?[] arguments)
    {
        var method = typeof(MainWindowViewModel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException($"Private method '{name}' was not found.");
        method.Invoke(viewModel, arguments);
    }
}
