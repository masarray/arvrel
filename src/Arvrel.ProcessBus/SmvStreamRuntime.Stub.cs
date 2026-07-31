#if !ARIEC61850_SIBLING
namespace Arvrel.ProcessBus;

internal sealed class SmvStreamRuntime
{
    public void SetMeasurementContext(SmvMeasurementContext context)
        => ArgumentNullException.ThrowIfNull(context);
}
#endif
