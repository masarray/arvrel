using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class VirtualInjectionTableTextTests
{
    [TestMethod]
    public void SerializeThenParse_PreservesComplete4I4VTable()
    {
        var entries = Enum.GetValues<VirtualInjectionSignal>()
            .Select((signal, index) => new VirtualInjectionTableEntry(
                signal,
                index % 2 == 0,
                index + 0.5,
                index * 75 - 210))
            .ToArray();
        var original = new VirtualInjectionTableDocument(50, entries);

        var text = VirtualInjectionTableText.Serialize(original);
        var parsed = VirtualInjectionTableText.TryParse(text, out var document, out var error);

        Assert.IsTrue(parsed, error);
        Assert.IsNotNull(document);
        Assert.AreEqual(50, document.FrequencyHz!.Value, 1e-12);
        Assert.AreEqual(8, document.Entries.Count);
        foreach (var expected in entries)
        {
            var actual = document.Entries.Single(entry => entry.Signal == expected.Signal);
            Assert.AreEqual(expected.Enabled, actual.Enabled);
            Assert.AreEqual(expected.Rms, actual.Rms, 1e-12);
            Assert.AreEqual(
                VirtualInjectionChannel.NormalizeAngle(expected.AngleDegrees),
                actual.AngleDegrees,
                1e-12);
        }
    }

    [TestMethod]
    public void Parse_AcceptsSpreadsheetFriendlyAliasesAndOptionalFrequency()
    {
        const string text = """
Signal	Enabled	RMS	Angle
V L1-E	yes	63.5	0
V L2-E	yes	63.5	-120
V L3-E	yes	63.5	120
V N	no	0	0
I L1	on	1	30
I L2	on	1	-90
I L3	on	1	150
I N	off	0	0
""";

        var parsed = VirtualInjectionTableText.TryParse(text, out var document, out var error);

        Assert.IsTrue(parsed, error);
        Assert.IsNotNull(document);
        Assert.IsNull(document.FrequencyHz);
        Assert.AreEqual(30, document.Entries.Single(entry => entry.Signal == VirtualInjectionSignal.PhaseACurrent).AngleDegrees, 1e-12);
    }

    [TestMethod]
    public void Parse_RejectsIncompleteTableWithoutReturningPartialDocument()
    {
        const string text = """
Signal	On	RMS value	Angle (deg)
V L1-E	1	63.5	0
V L2-E	1	63.5	-120
""";

        var parsed = VirtualInjectionTableText.TryParse(text, out var document, out var error);

        Assert.IsFalse(parsed);
        Assert.IsNull(document);
        StringAssert.Contains(error, "Missing");
    }

    [TestMethod]
    public void Parse_RejectsDuplicateSignal()
    {
        var valid = VirtualInjectionTableText.Serialize(new VirtualInjectionTableDocument(
            50,
            Enum.GetValues<VirtualInjectionSignal>()
                .Select(signal => new VirtualInjectionTableEntry(signal, true, 1, 0))
                .ToArray()));
        var duplicateLine = "V L1-E\t1\t1\t0\tV\n";
        var text = valid + duplicateLine;

        var parsed = VirtualInjectionTableText.TryParse(text, out var document, out var error);

        Assert.IsFalse(parsed);
        Assert.IsNull(document);
        StringAssert.Contains(error, "more than once");
    }

    [TestMethod]
    public void Parse_RejectsOutOfRangeFrequency()
    {
        var text = VirtualInjectionTableText.Serialize(new VirtualInjectionTableDocument(
            50,
            Enum.GetValues<VirtualInjectionSignal>()
                .Select(signal => new VirtualInjectionTableEntry(signal, true, 1, 0))
                .ToArray()))
            .Replace("FrequencyHz\t50", "FrequencyHz\t75", StringComparison.Ordinal);

        var parsed = VirtualInjectionTableText.TryParse(text, out var document, out var error);

        Assert.IsFalse(parsed);
        Assert.IsNull(document);
        StringAssert.Contains(error, "40–70");
    }
}
