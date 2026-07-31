namespace Arvrel.App.Infrastructure;

public static class SiblingEngineStatus
{
#if ARIEC61850_SIBLING
    public static readonly bool IsAvailable = true;
    public const string Label = "ARIEC61850 SIBLING READY";
#else
    public static readonly bool IsAvailable = false;
    public const string Label = "P0 SIMULATION MODE";
#endif
}
