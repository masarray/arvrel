using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerVirtualInjectionP18SourceTests
{
    [TestMethod]
    public void Injector_ExposesBothWindingSidesAndIndependentNeutralChannels()
    {
        var ui = Read("src", "Arvrel.App", "MainWindow.TransformerVirtualInjection.cs");

        StringAssert.Contains(ui, "(\"HV\", \"IA\", false)");
        StringAssert.Contains(ui, "(\"HV\", \"IN / NGR\", true)");
        StringAssert.Contains(ui, "(\"LV\", \"IA\", false)");
        StringAssert.Contains(ui, "(\"LV\", \"IN / NGR\", true)");
        StringAssert.Contains(ui, "Independent NCT / NGR");
        StringAssert.Contains(ui, "never calculated 3I0");
    }

    [TestMethod]
    public void Injector_UsesTransformerRuntime_NotASecondProtectionEngine()
    {
        var runtime = Read("src", "Arvrel.ProcessBus", "TransformerVirtualInjectionRuntime.cs");
        var ui = Read("src", "Arvrel.App", "MainWindow.TransformerVirtualInjection.cs");

        StringAssert.Contains(runtime, "new TransformerProtectionRuntime(_configuration)");
        StringAssert.Contains(runtime, "_runtime.EvaluateSnapshots(");
        StringAssert.Contains(runtime, "NeutralCurrentAvailable = neutral.Enabled");
        Assert.IsFalse(ui.Contains("new TransformerProtectionEngine", StringComparison.Ordinal));
        Assert.IsFalse(ui.Contains("OperatingCurrentPu =", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SharedWaveformPhasorAndRelayLcdConsumeInjectedEvidence()
    {
        var ui = Read("src", "Arvrel.App", "MainWindow.TransformerVirtualInjection.cs");
        var integration = Read("src", "Arvrel.App", "MainWindow.TransformerVirtualInjectionIntegration.cs");

        StringAssert.Contains(ui, "SmvScope.Frame = new WaveformFrame(");
        StringAssert.Contains(ui, "PhasorDisplayProjector.Project(side.Measurement.Phasors");
        StringAssert.Contains(integration, "HV --CT--[ 87T ]--CT-- LV");
        StringAssert.Contains(integration, "phaseA.OperatingCurrentPu");
        StringAssert.Contains(integration, "measurement.NeutralCurrentAvailable");
    }

    [TestMethod]
    public void LiveReplayDoesNotReceiveSyntheticWaitingSnapshot()
    {
        var integration = Read("src", "Arvrel.App", "MainWindow.TransformerVirtualInjectionIntegration.cs");

        StringAssert.Contains(integration, "Never publish/reset the synthetic runtime while Live Capture or PCAP Replay");
        StringAssert.Contains(integration, "if (internalSource)");
        StringAssert.Contains(integration, "SetTransformerInjectionWorkspaceActive(true);");
        StringAssert.Contains(integration, "_transformerInjectionView.Visibility = Visibility.Collapsed;");
        Assert.IsFalse(integration.Contains("SetTransformerInjectionWorkspaceActive(internalSource);", StringComparison.Ordinal));
    }

    private static string Read(params string[] segments)
        => File.ReadAllText(Locate(segments));

    private static string Locate(params string[] segments)
    {
        var starts = new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory)
        };

        foreach (var start in starts)
        {
            for (var current = start; current is not null; current = current.Parent)
            {
                var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException($"Unable to locate {Path.Combine(segments)} from the test workspace.");
    }
}
