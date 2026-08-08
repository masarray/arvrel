namespace Arvrel.Protection.Algorithms;

public sealed record AlgorithmDefinitionIdentity(
    string Element,
    string Version,
    string SourceHash,
    string SettingsFingerprint,
    string AuthorNote,
    DateTimeOffset StagedUtc,
    DateTimeOffset? ActivatedUtc,
    string OutputBoundary);

public sealed record AlgorithmActivationEvent(
    DateTimeOffset Timestamp,
    string Action,
    string Element,
    string? SourceHash,
    string? Version,
    string AuthorNote,
    long Generation);

public sealed record AlgorithmRuntimeRegistrySnapshot(
    long Generation,
    IReadOnlyList<AlgorithmDefinitionIdentity> Staged,
    IReadOnlyList<AlgorithmDefinitionIdentity> Active,
    IReadOnlyList<AlgorithmActivationEvent> AuditTrail)
{
    public bool HasActiveCustom => Active.Count > 0;
    public string Mode => HasActiveCustom ? "custom-active/standard-shadow" : Staged.Count > 0 ? "standard-active/custom-shadow" : "standard-active";
}

public sealed record AlgorithmAbComparisonSnapshot(
    DateTimeOffset Timestamp,
    string Element,
    string StandardSourceHash,
    string CustomSourceHash,
    bool CustomActive,
    ElementSnapshot Standard,
    ElementSnapshot Custom,
    bool StandardTripRequested,
    bool CustomTripRequested)
{
    public bool Diverged =>
        Standard.Pickup != Custom.Pickup ||
        Standard.Operated != Custom.Operated ||
        StandardTripRequested != CustomTripRequested;
}

/// <summary>
/// Process-local activation control plane for Research Mode. Definitions are immutable
/// and content-addressed. This registry never exposes a physical/GOOSE/MMS output path;
/// activation only changes which deterministic result ResearchProtectionEngine may use
/// for its existing virtual-trip projection.
/// </summary>
public static class AlgorithmRuntimeRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, RuntimeEntry> Staged = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RuntimeEntry> Active = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Stack<RuntimeEntry?>> Rollback = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<AlgorithmActivationEvent> Audit = new();
    private static long _generation;

    public static AlgorithmDefinitionIdentity Stage(
        string element,
        string source,
        ProtectionSettings settings,
        string? authorNote = null)
    {
        var compiled = AlgorithmSandboxCompiler.Compile(element, source, settings);
        var normalizedNote = NormalizeNote(authorNote);
        var now = DateTimeOffset.UtcNow;
        var identity = new AlgorithmDefinitionIdentity(
            element,
            $"custom-{compiled.SourceHash[..12]}",
            compiled.SourceHash,
            settings.Fingerprint(),
            normalizedNote,
            now,
            null,
            "virtual-only");

        lock (Gate)
        {
            Staged[element] = new RuntimeEntry(identity, compiled);
            var generation = ++_generation;
            AppendAudit(new AlgorithmActivationEvent(
                now,
                "STAGE",
                element,
                identity.SourceHash,
                identity.Version,
                identity.AuthorNote,
                generation));
            return identity;
        }
    }

    public static AlgorithmDefinitionIdentity Activate(
        string element,
        string sourceHash,
        ProtectionSettings settings,
        string? authorNote = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        var currentSettingsFingerprint = settings.Fingerprint();
        lock (Gate)
        {
            if (!Staged.TryGetValue(element, out var staged) ||
                !string.Equals(staged.Identity.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only the exact validated staged definition can be activated.");
            }
            if (!string.Equals(staged.Identity.SettingsFingerprint, currentSettingsFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Active protection settings changed after staging; compile and stage the definition again for the current settings fingerprint.");
            }

            if (!Rollback.TryGetValue(element, out var history))
            {
                history = new Stack<RuntimeEntry?>();
                Rollback[element] = history;
            }
            history.Push(Active.TryGetValue(element, out var previous) ? previous : null);

            var now = DateTimeOffset.UtcNow;
            var note = string.IsNullOrWhiteSpace(authorNote) ? staged.Identity.AuthorNote : NormalizeNote(authorNote);
            var activatedIdentity = staged.Identity with
            {
                ActivatedUtc = now,
                AuthorNote = note
            };
            var activated = staged with { Identity = activatedIdentity };
            Active[element] = activated;
            var generation = ++_generation;
            AppendAudit(new AlgorithmActivationEvent(
                now,
                "ACTIVATE",
                element,
                activatedIdentity.SourceHash,
                activatedIdentity.Version,
                note,
                generation));
            return activatedIdentity;
        }
    }

    public static AlgorithmDefinitionIdentity? RollbackElement(string element, string? authorNote = null)
    {
        lock (Gate)
        {
            if (!Active.ContainsKey(element))
                return null;

            RuntimeEntry? restored = null;
            if (Rollback.TryGetValue(element, out var history) && history.Count > 0)
                restored = history.Pop();

            if (restored is null)
                Active.Remove(element);
            else
                Active[element] = restored;

            var now = DateTimeOffset.UtcNow;
            var generation = ++_generation;
            var note = NormalizeNote(authorNote);
            AppendAudit(new AlgorithmActivationEvent(
                now,
                "ROLLBACK",
                element,
                restored?.Identity.SourceHash,
                restored?.Identity.Version,
                note,
                generation));
            return restored?.Identity;
        }
    }

    public static bool IsActive(string element, string? sourceHash = null)
    {
        lock (Gate)
            return Active.TryGetValue(element, out var entry) &&
                   (sourceHash is null || string.Equals(entry.Identity.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase));
    }

    public static AlgorithmRuntimeRegistrySnapshot Snapshot()
    {
        lock (Gate)
        {
            return new AlgorithmRuntimeRegistrySnapshot(
                _generation,
                Staged.Values.Select(entry => entry.Identity).OrderBy(identity => identity.Element, StringComparer.Ordinal).ToArray(),
                Active.Values.Select(entry => entry.Identity).OrderBy(identity => identity.Element, StringComparer.Ordinal).ToArray(),
                Audit.ToArray());
        }
    }

    public static void ClearSession(string? authorNote = null)
    {
        lock (Gate)
        {
            Staged.Clear();
            Active.Clear();
            Rollback.Clear();
            var generation = ++_generation;
            AppendAudit(new AlgorithmActivationEvent(
                DateTimeOffset.UtcNow,
                "CLEAR_SESSION",
                "*",
                null,
                null,
                NormalizeNote(authorNote),
                generation));
        }
    }

    internal static RuntimeExecutionPlan GetExecutionPlan()
    {
        lock (Gate)
        {
            var elements = Staged.Keys.Concat(Active.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var entries = new List<RuntimePlanEntry>(elements.Length);
            foreach (var element in elements)
            {
                Staged.TryGetValue(element, out var staged);
                Active.TryGetValue(element, out var active);
                var comparison = active ?? staged;
                if (comparison is not null)
                    entries.Add(new RuntimePlanEntry(element, comparison, active));
            }
            return new RuntimeExecutionPlan(_generation, entries);
        }
    }

    private static string NormalizeNote(string? value)
    {
        var note = string.IsNullOrWhiteSpace(value) ? "No author note supplied." : value.Trim();
        return note.Length <= 240 ? note : note[..240];
    }

    private static void AppendAudit(AlgorithmActivationEvent item)
    {
        Audit.Enqueue(item);
        while (Audit.Count > 128)
            Audit.Dequeue();
    }

    internal sealed record RuntimeEntry(AlgorithmDefinitionIdentity Identity, CompiledAlgorithm Compiled);
    internal sealed record RuntimePlanEntry(string Element, RuntimeEntry Comparison, RuntimeEntry? Active);
    internal sealed record RuntimeExecutionPlan(long Generation, IReadOnlyList<RuntimePlanEntry> Entries);
}

internal sealed class AlgorithmDualModeHost
{
    private readonly Dictionary<string, RuntimeInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private long _generation = -1;

    public IReadOnlyDictionary<string, AlgorithmExecutionResult> Evaluate(MeasurementFrame frame, ProtectionSettings settings)
    {
        var plan = AlgorithmRuntimeRegistry.GetExecutionPlan();
        if (_generation != plan.Generation)
        {
            Reconcile(plan);
            _generation = plan.Generation;
        }

        var results = new Dictionary<string, AlgorithmExecutionResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in plan.Entries)
        {
            var key = Key(entry.Element, entry.Comparison.Identity.SourceHash);
            if (!_instances.TryGetValue(key, out var runtime))
            {
                runtime = new RuntimeInstance(entry.Comparison.Identity, entry.Comparison.Compiled.CreateInstance());
                _instances[key] = runtime;
            }
            results[entry.Element] = runtime.Instance.Evaluate(frame, settings);
        }
        return results;
    }

    public void Reset()
    {
        foreach (var runtime in _instances.Values)
            runtime.Instance.Reset();
    }

    public void ObserveRejectedFrameTimestamp(DateTimeOffset timestamp)
    {
        // A rejected Ethernet frame must never advance a custom protection timer from
        // stale measurements. Resetting only custom dynamic state is conservative and
        // deterministic; the native reference engine independently advances its time
        // anchor while SMV trust remains blocked by the process-bus ingress policy.
        _ = timestamp;
        foreach (var runtime in _instances.Values)
            runtime.Instance.Reset();
    }

    private void Reconcile(AlgorithmRuntimeRegistry.RuntimeExecutionPlan plan)
    {
        var keep = plan.Entries.Select(entry => Key(entry.Element, entry.Comparison.Identity.SourceHash)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _instances.Keys.Where(key => !keep.Contains(key)).ToArray())
            _instances.Remove(stale);
    }

    private static string Key(string element, string hash) => $"{element}:{hash}";
    private sealed record RuntimeInstance(AlgorithmDefinitionIdentity Identity, AlgorithmInstance Instance);
}
