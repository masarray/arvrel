using System.Globalization;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class ProtectionSettingsIdentityTests
{
    [TestMethod]
    public void Fingerprint_IsIdenticalAcrossCultures()
    {
        var settings = new ProtectionSettings
        {
            GroupName = "GROUP A",
            Revision = 7,
            PhaseInstantaneousPickupA = 1.25,
            PhaseTimeMultiplier = 0.12,
            EarthTimePickupA = 0.30,
            Feeder = new FeederProtectionSettings
            {
                DirectionalPhase67Enabled = true,
                DirectionalPhase67PickupA = 1.25,
                DirectionalPhase67CharacteristicAngleDeg = 45.5
            }
        };
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var english = settings.Fingerprint();

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            var german = settings.Fingerprint();

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("id-ID");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("id-ID");
            var indonesian = settings.Fingerprint();

            Assert.AreEqual(english, german);
            Assert.AreEqual(english, indonesian);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [TestMethod]
    public void Validate_RejectsUndefinedLegacyCurveAndResetEnums()
    {
        var invalidCurve = new ProtectionSettings
        {
            PhaseTimeCurve = (IecCurveFamily)999
        };
        var invalidReset = new ProtectionSettings
        {
            EarthTimeResetMode = (ProtectionResetMode)999
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(invalidCurve.Validate);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(invalidReset.Validate);
    }

    [TestMethod]
    public void Validate_RejectsUndefinedFeederEnums()
    {
        var invalidMode = new ProtectionSettings
        {
            Feeder = new FeederProtectionSettings
            {
                Undervoltage27Mode = (VoltageMeasurementMode)999
            }
        };
        var invalidDirection = new ProtectionSettings
        {
            Feeder = new FeederProtectionSettings
            {
                DirectionalEarth67NSense = (DirectionalSense)999
            }
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(invalidMode.Validate);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(invalidDirection.Validate);
    }
}
