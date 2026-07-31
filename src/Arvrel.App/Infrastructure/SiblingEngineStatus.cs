namespace Arvrel.App.Infrastructure;

public static class SiblingEngineStatus
{
#if ARIEC61850_SIBLING
    public const bool IsAvailable = true;
    public const string Label = "ARIEC61850 SIBLING READY";
#else
    public const bool IsAvailable = false;
    public const string Label = "P0 SIMULATION MODE";
#endif
}
