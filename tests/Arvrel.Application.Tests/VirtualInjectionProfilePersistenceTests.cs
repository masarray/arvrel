using System.Text.Json;
using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Application.Tests;

[TestClass]
public sealed class VirtualInjectionProfilePersistenceTests
{
    [TestMethod]
    public void VersionedRoundTripPreservesCompleteConfigurationButNotRuntimeState()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var json = VirtualInjectionProfilePersistence.Serialize(
            profile,
            "unit-test",
            DateTimeOffset.UnixEpoch);

        StringAssert.Contains(json, "\"schemaVersion\": 1");
        StringAssert.Contains(json, "\"profileFingerprint\"");
        Assert.IsFalse(json.Contains("fluxLinkageVoltSeconds", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("sourceSampleIndex", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("processedSampleCount", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("tripLatched", StringComparison.Ordinal));

        var loaded = VirtualInjectionProfilePersistence.Deserialize(json);

        Assert.IsFalse(loaded.Migrated);
        Assert.AreEqual(1, loaded.SourceSchemaVersion);
        Assert.AreEqual("unit-test", loaded.Provenance);
        Assert.AreEqual(profile.Fingerprint(), loaded.Fingerprint);
        Assert.AreEqual(profile, loaded.Profile);
        Assert.AreEqual(profile.CurrentTransformer, loaded.Profile.CurrentTransformer);
        Assert.AreEqual(
            profile.PhaseACurrent.DcOffsetPercent,
            loaded.Profile.PhaseACurrent.DcOffsetPercent,
            1e-12);
        Assert.AreEqual(
            profile.PhaseACurrent.DcTimeConstantMilliseconds,
            loaded.Profile.PhaseACurrent.DcTimeConstantMilliseconds,
            1e-12);
    }

    [TestMethod]
    public void AtomicSaveCanReplaceFileAndLeavesNoTemporaryPayload()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"arvrel-profile-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "study.arvrel-injection.json");
        try
        {
            var first = VirtualInjectionPresets.Create("Normal balanced");
            var second = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
            VirtualInjectionProfilePersistence.SaveAtomic(path, first, "first", DateTimeOffset.UnixEpoch);
            VirtualInjectionProfilePersistence.SaveAtomic(path, second, "second", DateTimeOffset.UnixEpoch.AddSeconds(1));

            var loaded = VirtualInjectionProfilePersistence.LoadFile(path);
            Assert.AreEqual(second.Fingerprint(), loaded.Fingerprint);
            Assert.AreEqual("second", loaded.Provenance);
            CollectionAssert.AreEqual(
                new[] { Path.GetFileName(path) },
                Directory.GetFiles(directory).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void FutureSchemaTamperedFingerprintAndUnknownFieldsAreRejected()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var json = VirtualInjectionProfilePersistence.Serialize(profile, "unit-test", DateTimeOffset.UnixEpoch);

        var future = json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal);
        Assert.ThrowsException<InvalidDataException>(() => VirtualInjectionProfilePersistence.Deserialize(future));

        var tamperedFingerprint = json.Replace(
            profile.Fingerprint(),
            new string('0', 64),
            StringComparison.Ordinal);
        Assert.ThrowsException<InvalidDataException>(() => VirtualInjectionProfilePersistence.Deserialize(tamperedFingerprint));

        var unknown = json.Insert(json.IndexOf('{') + 1, "\n  \"unexpected\": true,");
        Assert.ThrowsException<InvalidDataException>(() => VirtualInjectionProfilePersistence.Deserialize(unknown));
    }

    [TestMethod]
    public void LegacyRawProfileMigratesWithoutInventingRuntimeState()
    {
        var profile = VirtualInjectionPresets.Create("CT saturation - A-G asymmetrical");
        var legacyJson = JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IgnoreReadOnlyProperties = true,
            WriteIndented = true
        });

        var loaded = VirtualInjectionProfilePersistence.Deserialize(legacyJson);

        Assert.IsTrue(loaded.Migrated);
        Assert.AreEqual(0, loaded.SourceSchemaVersion);
        Assert.AreEqual("legacy-raw-profile", loaded.Provenance);
        Assert.AreEqual(profile.Fingerprint(), loaded.Fingerprint);
        Assert.AreEqual(profile, loaded.Profile);
    }
}
