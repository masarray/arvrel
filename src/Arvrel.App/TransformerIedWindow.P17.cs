using Arvrel.ProcessBus;

namespace Arvrel.App;

public partial class TransformerIedWindow
{
    private bool _p17FaceplateBridgeInitialized;
    private TransformerProtectionRuntimeSnapshot? _p17LastPublishedSnapshot;
    private EventHandler<TransformerProtectionRuntimeSnapshotChangedEventArgs>? _faceplateSnapshotChanged;

    /// <summary>
    /// Presentation bridge for the P17 operator faceplate. The practitioner window
    /// remains the owner of TransformerProcessBusProtectionRuntime; this event only
    /// publishes snapshots that have already been produced by that authority.
    /// </summary>
    internal event EventHandler<TransformerProtectionRuntimeSnapshotChangedEventArgs>? FaceplateSnapshotChanged
    {
        add
        {
            _faceplateSnapshotChanged += value;
            InitializeP17FaceplateBridge();
            PublishFaceplateSnapshot(force: true);
        }
        remove => _faceplateSnapshotChanged -= value;
    }

    internal TransformerProtectionRuntimeSnapshot? FaceplateSnapshot => _lastSnapshot;

    internal void InitializeP17FaceplateBridge()
    {
        if (_p17FaceplateBridgeInitialized)
            return;

        _p17FaceplateBridgeInitialized = true;
        // TransformerIedWindow registered its normal evaluation tick in the constructor.
        // P17 is initialized afterwards, so this callback runs later in registration order
        // and only publishes the snapshot that P11/P12 already evaluated.
        _refreshTimer.Tick += P17FaceplateRefreshTimer_Tick;
        Closed += P17FaceplateBridge_Closed;
        PublishFaceplateSnapshot(force: true);
    }

    internal bool TryResetRuntimeFromFaceplate(out string message)
    {
        if (_runtime is null)
        {
            message = "No transformer runtime is active.";
            return false;
        }

        _runtime.Reset();
        var snapshot = _runtime.CurrentSnapshot;
        RenderSnapshot(snapshot);
        PublishFaceplateSnapshot(force: true);
        message = "Transformer pickup timers and virtual trip latch reset from the virtual relay front panel.";
        return true;
    }

    private void P17FaceplateRefreshTimer_Tick(object? sender, EventArgs e)
        => PublishFaceplateSnapshot(force: false);

    private void PublishFaceplateSnapshot(bool force)
    {
        var snapshot = _lastSnapshot;
        if (snapshot is null)
            return;

        if (!force && ReferenceEquals(snapshot, _p17LastPublishedSnapshot))
            return;

        _p17LastPublishedSnapshot = snapshot;
        _faceplateSnapshotChanged?.Invoke(this, new TransformerProtectionRuntimeSnapshotChangedEventArgs(snapshot));
    }

    private void P17FaceplateBridge_Closed(object? sender, EventArgs e)
    {
        _refreshTimer.Tick -= P17FaceplateRefreshTimer_Tick;
        Closed -= P17FaceplateBridge_Closed;
        _p17FaceplateBridgeInitialized = false;
        _p17LastPublishedSnapshot = null;
    }
}
