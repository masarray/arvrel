namespace Arvrel.Capture;

/// <summary>
/// Stable adapter identity exposed by any live packet-capture backend.
/// Backend-specific handles remain opaque to the process-bus pipeline.
/// </summary>
public sealed record CaptureAdapter(
    string Id,
    string DisplayName,
    string Address,
    string BackendId)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// One captured data-link frame with source timestamp and optional interface identity.
/// </summary>
public sealed record CapturedFrame(
    DateTimeOffset Timestamp,
    ReadOnlyMemory<byte> Frame,
    int InterfaceId = 0);

public sealed record CaptureOptions
{
    public string Filter { get; init; } = string.Empty;
    public int BufferCapacity { get; init; } = 16_384;
    public int ReadTimeoutMilliseconds { get; init; } = 250;

    public void Validate()
    {
        if (BufferCapacity is < 1 or > 1_048_576)
            throw new ArgumentOutOfRangeException(nameof(BufferCapacity));
        if (ReadTimeoutMilliseconds is < 1 or > 60_000)
            throw new ArgumentOutOfRangeException(nameof(ReadTimeoutMilliseconds));
    }
}

/// <summary>
/// Platform-neutral live capture contract. Implementations may use Npcap,
/// libpcap, BPF, or another backend without leaking those APIs upstream.
/// </summary>
public interface ILiveCaptureBackend
{
    string BackendId { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }

    IReadOnlyList<CaptureAdapter> ListAdapters();

    IAsyncEnumerable<CapturedFrame> CaptureAsync(
        string adapterId,
        CaptureOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Portable replay contract for capture files. The process-bus pipeline consumes
/// frames and does not own a specific PCAP parser implementation.
/// </summary>
public interface ICaptureReplaySource
{
    string SourceId { get; }
    bool CanRead(string path);

    IAsyncEnumerable<CapturedFrame> ReadAsync(
        string path,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableLiveCaptureBackend : ILiveCaptureBackend
{
    public UnavailableLiveCaptureBackend(string backendId, string displayName, string availabilityMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backendId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(availabilityMessage);

        BackendId = backendId;
        DisplayName = displayName;
        AvailabilityMessage = availabilityMessage;
    }

    public string BackendId { get; }
    public string DisplayName { get; }
    public bool IsAvailable => false;
    public string AvailabilityMessage { get; }

    public IReadOnlyList<CaptureAdapter> ListAdapters()
        => Array.Empty<CaptureAdapter>();

    public IAsyncEnumerable<CapturedFrame> CaptureAsync(
        string adapterId,
        CaptureOptions options,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException(AvailabilityMessage);
}
