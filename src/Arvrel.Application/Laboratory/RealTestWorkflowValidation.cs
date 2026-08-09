namespace Arvrel.Application.Laboratory;

internal static class RealTestWorkflowValidation
{
    internal static void ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Workflow name is required.");
    }

    internal static void ValidateRms(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1_000_000_000)
            throw new ArgumentOutOfRangeException(name, "RMS value must be finite and between 0 and 1e9.");
    }

    internal static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    internal static void ValidateDwell(TimeSpan value, string name, TimeSpan? maximum = null)
    {
        var max = maximum ?? TimeSpan.FromSeconds(5);
        if (value < ClosedLoopVirtualTestBench.SimulationQuantum || value > max)
            throw new ArgumentOutOfRangeException(name, $"Duration must be between {ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds:0.###} ms and {max.TotalSeconds:0.###} s.");
    }
}
