using System.Runtime.CompilerServices;
using Arvrel.Capture;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.ProcessBus.Tests;

[TestClass]
public sealed class CaptureBackendInjectionTests
{
    [TestMethod]
    public async Task ControllerUsesInjectedLiveBackendWithoutBackendSpecificTypes()
    {
        var backend = new FakeLiveCaptureBackend();
        await using var controller = new SmvProcessBusController(
            new ProtectionSettings(),
            backend,
            new EmptyReplaySource());

        var adapters = controller.ListAdapters();

        Assert.IsTrue(controller.IsLiveCaptureAvailable);
        Assert.AreEqual("fake", controller.LiveCaptureBackendId);
        Assert.AreEqual("Fake capture", controller.LiveCaptureBackendName);
        Assert.AreEqual(1, adapters.Count);
        Assert.AreEqual("adapter-a", adapters[0].Selector);
        Assert.AreEqual("Adapter A", adapters[0].DisplayName);
        Assert.AreEqual("00-11-22-33-44-55", adapters[0].MacAddress);
    }

    [TestMethod]
    public async Task ControllerReportsUnavailableLiveBackendWithoutAffectingReplayContract()
    {
        var backend = new UnavailableLiveCaptureBackend(
            "none",
            "No backend",
            "Native capture is unavailable.");
        await using var controller = new SmvProcessBusController(
            new ProtectionSettings(),
            backend,
            new EmptyReplaySource());

        Assert.IsFalse(controller.IsLiveCaptureAvailable);
        Assert.AreEqual(0, controller.ListAdapters().Count);
        Assert.AreEqual("Native capture is unavailable.", controller.CaptureAvailabilityMessage);
    }

    private sealed class FakeLiveCaptureBackend : ILiveCaptureBackend
    {
        public string BackendId => "fake";
        public string DisplayName => "Fake capture";
        public bool IsAvailable => true;
        public string AvailabilityMessage => "Fake backend ready.";

        public IReadOnlyList<CaptureAdapter> ListAdapters()
            => [new CaptureAdapter(
                "adapter-a",
                "Adapter A",
                "00-11-22-33-44-55",
                BackendId)];

        public async IAsyncEnumerable<CapturedFrame> CaptureAsync(
            string adapterId,
            CaptureOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }
    }

    private sealed class EmptyReplaySource : ICaptureReplaySource
    {
        public string SourceId => "empty";

        public bool CanRead(string path) => true;

        public async IAsyncEnumerable<CapturedFrame> ReadAsync(
            string path,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }
    }
}
