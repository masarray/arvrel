using System.Globalization;
using System.Runtime.CompilerServices;
using Arvrel.Capture;

#if ARIEC61850_SIBLING
using AR.Iec61850.Transports;
using AR.Iec61850.Transports.Npcap;
#endif

namespace Arvrel.ProcessBus.Capture;

internal static class ProcessBusCaptureBackendFactory
{
#if ARIEC61850_SIBLING
    public static bool DecoderAvailable => true;
#else
    public static bool DecoderAvailable => false;
#endif

    public static ILiveCaptureBackend CreateDefault()
    {
#if ARIEC61850_SIBLING
        return new NpcapLiveCaptureBackend();
#else
        return new UnavailableLiveCaptureBackend(
            "unavailable",
            "Live capture unavailable",
            "Live capture requires the sibling ARIEC61850 engine and a supported native capture backend.");
#endif
    }
}

#if ARIEC61850_SIBLING
internal sealed class NpcapLiveCaptureBackend : ILiveCaptureBackend
{
    public string BackendId => "npcap";
    public string DisplayName => "Npcap";
    public bool IsAvailable => true;
    public string AvailabilityMessage => "Npcap capture backend is available.";

    public IReadOnlyList<CaptureAdapter> ListAdapters()
        => NpcapAdapterCatalog.ListAdapters()
            .Select(adapter => new CaptureAdapter(
                adapter.Index.ToString(CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(adapter.Description)
                    ? $"{adapter.Index}. {adapter.Name}"
                    : $"{adapter.Index}. {adapter.Description}",
                adapter.MacAddress?.ToString() ?? "No MAC",
                BackendId))
            .ToArray();

    public async IAsyncEnumerable<CapturedFrame> CaptureAsync(
        string adapterId,
        CaptureOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using var source = new NpcapProcessBusFrameSource(adapterId);
        var nativeOptions = new ProcessBusCaptureOptions
        {
            Filter = options.Filter,
            BufferCapacity = options.BufferCapacity,
            ReadTimeoutMilliseconds = options.ReadTimeoutMilliseconds
        };

        await foreach (var captured in source.CaptureAsync(nativeOptions, cancellationToken).ConfigureAwait(false))
        {
            yield return new CapturedFrame(
                captured.Timestamp,
                captured.Frame);
        }
    }
}
#endif
