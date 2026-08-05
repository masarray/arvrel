using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class RelayLcdPhasorStabilizerTests
{
    [TestMethod]
    public void SubQuantumVariations_ProduceIdenticalPresentationSignature()
    {
        var first = RelayLcdPhasorStabilizer.Canonicalize(Frame(
            new PhasorDisplayVector("IA", "R / IA", "I", 1.001, 0.11),
            new PhasorDisplayVector("IB", "S / IB", "I", 0.999, -119.88),
            new PhasorDisplayVector("IC", "T / IC", "I", 1.002, 120.12)));
        var second = RelayLcdPhasorStabilizer.Canonicalize(Frame(
            new PhasorDisplayVector("IA", "R / IA", "I", 1.004, 0.19),
            new PhasorDisplayVector("IB", "S / IB", "I", 1.003, -119.79),
            new PhasorDisplayVector("IC", "T / IC", "I", 0.997, 120.20)));

        Assert.AreEqual(
            RelayLcdPhasorStabilizer.Signature(first),
            RelayLcdPhasorStabilizer.Signature(second));
    }

    [TestMethod]
    public void MeaningfulAngleChange_ChangesPresentationSignature()
    {
        var first = RelayLcdPhasorStabilizer.Canonicalize(Frame(
            new PhasorDisplayVector("IA", "R / IA", "I", 1, 0),
            new PhasorDisplayVector("IB", "S / IB", "I", 1, -120),
            new PhasorDisplayVector("IC", "T / IC", "I", 1, 120)));
        var second = RelayLcdPhasorStabilizer.Canonicalize(Frame(
            new PhasorDisplayVector("IA", "R / IA", "I", 1, 3),
            new PhasorDisplayVector("IB", "S / IB", "I", 1, -117),
            new PhasorDisplayVector("IC", "T / IC", "I", 1, 123)));

        Assert.AreNotEqual(
            RelayLcdPhasorStabilizer.Signature(first),
            RelayLcdPhasorStabilizer.Signature(second));
    }

    [TestMethod]
    public void Canonicalize_RecomputesMaximumCurrentFromVisibleVectors()
    {
        var canonical = RelayLcdPhasorStabilizer.Canonicalize(Frame(
            new PhasorDisplayVector("IA", "R / IA", "I", 1.004, 0),
            new PhasorDisplayVector("IB", "S / IB", "I", 0.994, -120),
            new PhasorDisplayVector("IC", "T / IC", "I", 1.016, 120)));

        Assert.AreEqual(1.02, canonical.MaximumCurrent, 1e-9);
        Assert.AreEqual(1.02, canonical.Vectors.Max(vector => vector.Magnitude), 1e-9);
    }

    [TestMethod]
    public void UnavailableFrame_RemainsUnavailable()
    {
        var unavailable = PhasorDisplayFrame.Unavailable(PhasorDisplayMode.Current, "No coherent frame");
        var canonical = RelayLcdPhasorStabilizer.Canonicalize(unavailable);

        Assert.IsFalse(canonical.IsAvailable);
        Assert.AreEqual("No coherent frame", canonical.Status);
        Assert.AreEqual(
            RelayLcdPhasorStabilizer.Signature(unavailable),
            RelayLcdPhasorStabilizer.Signature(canonical));
    }

    private static PhasorDisplayFrame Frame(params PhasorDisplayVector[] vectors)
        => new(
            PhasorDisplayMode.Current,
            vectors,
            "VA = 0°",
            0,
            vectors.Where(vector => vector.QuantityKind == "I").Max(vector => vector.Magnitude),
            0,
            0,
            double.NaN,
            50.001,
            true,
            "IA · IB · IC · 3I0");
}
