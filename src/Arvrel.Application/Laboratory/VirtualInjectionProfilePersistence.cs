using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

public sealed record VirtualInjectionProfileDocument(
    int SchemaVersion,
    string Kind,
    DateTimeOffset SavedAt,
    string ProfileFingerprint,
    string Provenance,
    VirtualInjectionProfile Profile);

public sealed record VirtualInjectionProfileLoadResult(
    VirtualInjectionProfile Profile,
    int SourceSchemaVersion,
    bool Migrated,
    string Provenance,
    string Fingerprint);

/// <summary>
/// Versioned persistence for reproducible virtual-injection configuration.
/// Runtime CT flux, source sample time, timers, and relay latches are deliberately
/// excluded and must never be restored by opening a profile file.
/// </summary>
public static class VirtualInjectionProfilePersistence
{
    public const int CurrentSchemaVersion = 1;
    public const string DocumentKind = "arvrel.virtual-injection-profile";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        IgnoreReadOnlyProperties = true,
        WriteIndented = true
    };

    public static string Serialize(
        VirtualInjectionProfile profile,
        string provenance,
        DateTimeOffset? savedAt = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(provenance))
            throw new ArgumentException("Profile provenance is required.", nameof(provenance));

        profile = profile.Normalize();
        var document = new VirtualInjectionProfileDocument(
            CurrentSchemaVersion,
            DocumentKind,
            savedAt ?? DateTimeOffset.UtcNow,
            profile.Fingerprint(),
            provenance.Trim(),
            profile);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static VirtualInjectionProfileLoadResult Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("Virtual-injection profile JSON is empty.");

        try
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Virtual-injection profile root must be a JSON object.");
            RejectDuplicateProperties(parsed.RootElement, "$");

            return parsed.RootElement.TryGetProperty("schemaVersion", out _)
                ? DeserializeDocument(json)
                : DeserializeLegacyRawProfile(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Virtual-injection profile JSON is malformed or contains unsupported fields.", ex);
        }
    }

    public static void SaveAtomic(
        string path,
        VirtualInjectionProfile profile,
        string provenance,
        DateTimeOffset? savedAt = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Profile path is required.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Profile path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var payload = Serialize(profile, provenance, savedAt);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       16_384,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(payload);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static VirtualInjectionProfileLoadResult LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Profile path is required.", nameof(path));
        return Deserialize(File.ReadAllText(Path.GetFullPath(path), Encoding.UTF8));
    }

    private static void RejectDuplicateProperties(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                        throw new InvalidDataException($"Duplicate JSON property '{property.Name}' at {path}.");
                    RejectDuplicateProperties(property.Value, $"{path}.{property.Name}");
                }
                break;
            }
            case JsonValueKind.Array:
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    RejectDuplicateProperties(item, $"{path}[{index}]");
                    index++;
                }
                break;
            }
        }
    }

    private static VirtualInjectionProfileLoadResult DeserializeDocument(string json)
    {
        var document = JsonSerializer.Deserialize<VirtualInjectionProfileDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("Virtual-injection profile document is empty.");

        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                document.SchemaVersion > CurrentSchemaVersion
                    ? $"Profile schema {document.SchemaVersion} is newer than supported schema {CurrentSchemaVersion}."
                    : $"Profile schema {document.SchemaVersion} is unsupported; migrate it before loading.");
        if (!string.Equals(document.Kind, DocumentKind, StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected profile document kind '{document.Kind}'.");
        if (document.SavedAt == default)
            throw new InvalidDataException("Profile savedAt timestamp is required.");
        if (string.IsNullOrWhiteSpace(document.Provenance))
            throw new InvalidDataException("Profile provenance is required.");
        if (document.Profile is null)
            throw new InvalidDataException("Profile payload is required.");

        var profile = document.Profile.Normalize();
        var fingerprint = profile.Fingerprint();
        if (!string.Equals(document.ProfileFingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Profile fingerprint does not match the normalized configuration payload.");

        return new VirtualInjectionProfileLoadResult(
            profile,
            document.SchemaVersion,
            false,
            document.Provenance.Trim(),
            fingerprint);
    }

    private static VirtualInjectionProfileLoadResult DeserializeLegacyRawProfile(string json)
    {
        var profile = JsonSerializer.Deserialize<VirtualInjectionProfile>(json, JsonOptions)
            ?? throw new InvalidDataException("Legacy virtual-injection profile payload is empty.");
        profile = profile.Normalize();
        return new VirtualInjectionProfileLoadResult(
            profile,
            0,
            true,
            "legacy-raw-profile",
            profile.Fingerprint());
    }
}
