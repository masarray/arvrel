using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class TransformerPublicSelfTestTests
{
    [TestMethod]
    public void PublicSuite_RunsTenDeterministicProtectionScenarios()
    {
        var report = TransformerPublicSelfTest.RunAll();

        Assert.AreEqual(TransformerPublicSelfTest.SuiteId, report.SuiteId);
        Assert.AreEqual(10, report.Cases.Count);
        Assert.AreEqual(10, report.PassedCount, report.ToPlainText());
        Assert.AreEqual(0, report.FailedCount, report.ToPlainText());
        Assert.IsTrue(report.AllPassed, report.ToPlainText());
    }

    [TestMethod]
    public void PublicSuite_ContainsCriticalOperateAndSecurityBoundaries()
    {
        var report = TransformerPublicSelfTest.RunAll();
        var ids = report.Cases.Select(test => test.Id).ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "87T-THROUGH",
                "87T-INTERNAL",
                "87T-HS",
                "H2-BLOCK",
                "H5-BLOCK",
                "P13-EXT-SAT",
                "P13-INTERNAL-DIST",
                "87N-HV",
                "87N-LV",
                "87N-NO-NEUTRAL"
            },
            ids);
        Assert.AreEqual(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void PublicSuite_ReportIsCopyableAndStatesSafetyBoundary()
    {
        var report = TransformerPublicSelfTest.RunAll();
        var text = report.ToPlainText();

        StringAssert.Contains(text, "Result: PASS · 10/10 passed");
        StringAssert.Contains(text, "P13-EXT-SAT");
        StringAssert.Contains(text, "P13-INTERNAL-DIST");
        StringAssert.Contains(text, "87N-NO-NEUTRAL");
        StringAssert.Contains(text, "not calibration, conformance, or protection-grade trip proof");
    }
}