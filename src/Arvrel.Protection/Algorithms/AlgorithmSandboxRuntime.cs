using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Arvrel.Protection.Algorithms;

public sealed class AlgorithmCompilationException : Exception
{
    public AlgorithmCompilationException(string message) : base(message) { }
}

public sealed record AlgorithmExecutionResult(
    string Element,
    ElementSnapshot Snapshot,
    bool TripRequested,
    bool OperationReached,
    int InstructionCount,
    string SourceHash);

public sealed record AlgorithmTestBenchResult(
    string Element,
    int Frames,
    int DivergentFrames,
    TimeSpan? StandardFirstPickup,
    TimeSpan? CustomFirstPickup,
    TimeSpan? StandardFirstTrip,
    TimeSpan? CustomFirstTrip,
    AlgorithmExecutionResult StandardFinal,
    AlgorithmExecutionResult CustomFinal);

/// <summary>
/// Compiles the intentionally small ARVREL research DSL into an in-process,
/// deterministic interpreter. The interpreter exposes only typed measurements,
/// active settings and SMV trust booleans; it has no host-object, file, network,
/// process, reflection or unmanaged capability.
/// </summary>
public static class AlgorithmSandboxCompiler
{
    public const int MaximumSourceBytes = 64 * 1024;
    public const int MaximumStatements = 64;
    public const int MaximumInstructionsPerFrame = 512;
    private static readonly HashSet<string> RuntimeElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "50P-1", "51P", "50N", "51N"
    };

    public static CompiledAlgorithm Compile(string element, string source, ProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        source ??= string.Empty;

        if (!RuntimeElements.Contains(element))
            throw new AlgorithmCompilationException($"Runtime activation is currently restricted to 50P-1, 51P, 50N and 51N; {element} remains shadow-only.");
        if (Encoding.UTF8.GetByteCount(source) > MaximumSourceBytes)
            throw new AlgorithmCompilationException($"Algorithm source exceeds the {MaximumSourceBytes:N0}-byte sandbox memory limit.");

        var policy = AlgorithmPolicyValidator.Validate(source);
        if (!policy.IsValid)
            throw new AlgorithmCompilationException(string.Join(Environment.NewLine, policy.Errors));

        var program = ProgramParser.Parse(element, source);
        if (program.Statements.Count > MaximumStatements)
            throw new AlgorithmCompilationException($"Algorithm has {program.Statements.Count} statements; the sandbox limit is {MaximumStatements}.");
        if (program.InstructionCost > MaximumInstructionsPerFrame)
            throw new AlgorithmCompilationException($"Algorithm requires {program.InstructionCost} instructions per frame; the sandbox limit is {MaximumInstructionsPerFrame}.");

        // Compile-time unit/name validation is performed by evaluating two harmless
        // deterministic frames. Unknown settings, bad units and invalid expression
        // types fail here, before a definition can be staged or activated.
        try
        {
            var instance = new AlgorithmInstance(program, policy.ContentHash);
            var t0 = DateTimeOffset.UnixEpoch;
            var frame = new MeasurementFrame(t0, 1, 1, 1, 0.1, SmvTrustState.Healthy);
            _ = instance.Evaluate(frame, settings);
            _ = instance.Evaluate(frame with { Timestamp = t0.AddMilliseconds(5), PhaseA = 8, Residual = 2 }, settings);
        }
        catch (AlgorithmRuntimeException ex)
        {
            throw new AlgorithmCompilationException($"Typed DSL compile failed: {ex.Message}");
        }

        return new CompiledAlgorithm(element, source, policy.ContentHash, program);
    }
}

public sealed class CompiledAlgorithm
{
    internal CompiledAlgorithm(string element, string source, string sourceHash, AlgorithmProgram program)
    {
        Element = element;
        Source = source;
        SourceHash = sourceHash;
        Program = program;
    }

    public string Element { get; }
    public string Source { get; }
    public string SourceHash { get; }
    public int InstructionCost => Program.InstructionCost;
    internal AlgorithmProgram Program { get; }

    public AlgorithmInstance CreateInstance() => new(Program, SourceHash);
}

public sealed class AlgorithmInstance
{
    private readonly AlgorithmProgram _program;
    private readonly string _sourceHash;
    private readonly RuntimeState _state = new();
    private DateTimeOffset? _previousTimestamp;

    internal AlgorithmInstance(AlgorithmProgram program, string sourceHash)
    {
        _program = program;
        _sourceHash = sourceHash;
    }

    public void Reset()
    {
        _state.Clear();
        _previousTimestamp = null;
    }

    public AlgorithmExecutionResult Evaluate(MeasurementFrame frame, ProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(frame.SmvTrust);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        var delta = ResolveDelta(frame.Timestamp);
        var enabled = SettingResolver.IsEnabled(_program.Element, settings);
        if (!frame.SmvTrust.AllowsMeasurement || !enabled)
        {
            _state.Clear();
            var disabled = new ElementSnapshot(
                ElementIds.Resolve(_program.Element),
                enabled ? ProtectionStageState.Blocked : ProtectionStageState.Disabled,
                false,
                false,
                0,
                OperatingQuantity(_program.Element, frame),
                SettingResolver.Pickup(_program.Element, settings),
                enabled ? "Custom runtime measurement blocked by SMV trust policy." : "Element disabled by active setting group.");
            return new AlgorithmExecutionResult(_program.Element, disabled, false, false, 0, _sourceHash);
        }

        var context = new EvaluationContext(_program.Element, frame, settings, delta, _state);
        foreach (var statement in _program.PreStateStatements)
            statement.Execute(context);

        if (context.TryGetBoolean("pickup", out var declaredPickup))
            context.Set("pickup", EvalValue.Boolean(declaredPickup && enabled && frame.SmvTrust.AllowsPickup));

        foreach (var statement in _program.StateStatements)
            statement.Execute(context);
        foreach (var statement in _program.TripStatements)
            statement.Execute(context);

        if (context.Instructions > AlgorithmSandboxCompiler.MaximumInstructionsPerFrame)
            throw new AlgorithmRuntimeException("Instruction budget exceeded; execution was terminated before output publication.");

        var pickup = context.TryGetBoolean("pickup", out var pickupValue)
            ? pickupValue && frame.SmvTrust.AllowsPickup
            : context.TryGetNumber("multiple", out var multiple) && multiple > 1 && frame.SmvTrust.AllowsPickup;
        var operationReached = context.TryGetBoolean("operate", out var operate)
            ? operate
            : context.TryGetNumber("progress", out var progressValue) && progressValue >= 1;
        var block = context.TryGetBoolean("block", out var blockedBySource) && blockedBySource;
        var tripExpression = context.TryGetBoolean("trip", out var trip) && trip;

        // Host boundary is deliberately stronger than source policy: even a valid
        // source cannot publish a virtual trip without SMV permission, and source
        // blocking is always fail-safe/additive.
        var tripRequested = operationReached && tripExpression && frame.SmvTrust.AllowsTrip && !block;
        var blocked = operationReached && (!frame.SmvTrust.AllowsTrip || block);
        var progress = Progress(context);
        var quantity = context.TryGetNumber("operatingCurrent", out var operatingCurrent)
            ? operatingCurrent
            : OperatingQuantity(_program.Element, frame);
        var pickupSetting = SettingResolver.Pickup(_program.Element, settings);
        var state = blocked
            ? ProtectionStageState.Blocked
            : operationReached
                ? ProtectionStageState.Operated
                : pickup
                    ? ProtectionStageState.Timing
                    : ProtectionStageState.Ready;
        var reason = blocked
            ? "Custom operation reached but virtual trip permission is blocked."
            : operationReached
                ? "Custom deterministic algorithm operated."
                : pickup
                    ? "Custom deterministic algorithm timing."
                    : "Custom deterministic algorithm ready.";
        var snapshot = new ElementSnapshot(
            ElementIds.Resolve(_program.Element),
            state,
            pickup,
            operationReached,
            Math.Clamp(progress, 0, 1),
            quantity,
            pickupSetting,
            reason);
        return new AlgorithmExecutionResult(
            _program.Element,
            snapshot,
            tripRequested,
            operationReached,
            context.Instructions,
            _sourceHash);
    }

    private TimeSpan ResolveDelta(DateTimeOffset timestamp)
    {
        var delta = _previousTimestamp is null ? TimeSpan.Zero : timestamp - _previousTimestamp.Value;
        _previousTimestamp = timestamp;
        return delta < TimeSpan.Zero || delta > TimeSpan.FromSeconds(1) ? TimeSpan.Zero : delta;
    }

    private double Progress(EvaluationContext context)
    {
        if (context.TryGetNumber("progress", out var progress))
            return progress;
        if (_state.Timers.TryGetValue("operate", out var timer))
        {
            var delay = timer.TargetSeconds;
            return delay <= 0 ? (timer.ElapsedSeconds > 0 ? 1 : 0) : timer.ElapsedSeconds / delay;
        }
        return 0;
    }

    private static double OperatingQuantity(string element, MeasurementFrame frame)
        => element is "50N" or "51N" ? Math.Max(0, frame.Residual) : Math.Max(0, frame.MaximumPhase);
}

public static class AlgorithmTestBench
{
    public static AlgorithmTestBenchResult Compare(
        string element,
        string standardSource,
        string customSource,
        ProtectionSettings settings,
        TimeSpan? duration = null,
        TimeSpan? step = null)
    {
        var standard = AlgorithmSandboxCompiler.Compile(element, standardSource, settings).CreateInstance();
        var custom = AlgorithmSandboxCompiler.Compile(element, customSource, settings).CreateInstance();
        var total = duration ?? TimeSpan.FromSeconds(2);
        var interval = step ?? TimeSpan.FromMilliseconds(5);
        if (total <= TimeSpan.Zero || interval <= TimeSpan.Zero || interval > TimeSpan.FromMilliseconds(100))
            throw new ArgumentOutOfRangeException(nameof(step));

        var pickupSetting = SettingResolver.Pickup(element, settings);
        var start = DateTimeOffset.UnixEpoch;
        TimeSpan? standardPickup = null;
        TimeSpan? customPickup = null;
        TimeSpan? standardTrip = null;
        TimeSpan? customTrip = null;
        var divergent = 0;
        var frames = 0;
        AlgorithmExecutionResult? standardResult = null;
        AlgorithmExecutionResult? customResult = null;

        for (var elapsed = TimeSpan.Zero; elapsed <= total; elapsed += interval)
        {
            var inFault = elapsed >= TimeSpan.FromMilliseconds(100) && elapsed < TimeSpan.FromMilliseconds(1500);
            var quantity = pickupSetting * (inFault ? 6.0 : 0.50);
            var frame = element is "50N" or "51N"
                ? new MeasurementFrame(start + elapsed, 0.5, 0.5, 0.5, quantity, SmvTrustState.Healthy)
                : new MeasurementFrame(start + elapsed, quantity, 0.5, 0.5, 0.05, SmvTrustState.Healthy);

            standardResult = standard.Evaluate(frame, settings);
            customResult = custom.Evaluate(frame, settings);
            frames++;
            if (standardResult.Snapshot.Pickup != customResult.Snapshot.Pickup ||
                standardResult.OperationReached != customResult.OperationReached ||
                standardResult.TripRequested != customResult.TripRequested)
                divergent++;

            if (standardResult.Snapshot.Pickup && standardPickup is null) standardPickup = elapsed;
            if (customResult.Snapshot.Pickup && customPickup is null) customPickup = elapsed;
            if (standardResult.TripRequested && standardTrip is null) standardTrip = elapsed;
            if (customResult.TripRequested && customTrip is null) customTrip = elapsed;
        }

        return new AlgorithmTestBenchResult(
            element,
            frames,
            divergent,
            standardPickup,
            customPickup,
            standardTrip,
            customTrip,
            standardResult!,
            customResult!);
    }
}

internal sealed class AlgorithmRuntimeException : Exception
{
    public AlgorithmRuntimeException(string message) : base(message) { }
}

internal sealed class AlgorithmProgram
{
    public AlgorithmProgram(string element, IReadOnlyList<Statement> statements)
    {
        Element = element;
        Statements = statements;
        PreStateStatements = statements.Where(statement => statement is AssignmentStatement assignment && !assignment.IsTrip).ToArray();
        StateStatements = statements.Where(statement => statement is PersistStatement or IntegrateStatement).ToArray();
        TripStatements = statements.Where(statement => statement is AssignmentStatement assignment && assignment.IsTrip).ToArray();
        InstructionCost = statements.Sum(statement => statement.InstructionCost);
    }

    public string Element { get; }
    public IReadOnlyList<Statement> Statements { get; }
    public IReadOnlyList<Statement> PreStateStatements { get; }
    public IReadOnlyList<Statement> StateStatements { get; }
    public IReadOnlyList<Statement> TripStatements { get; }
    public int InstructionCost { get; }
}

internal static class ProgramParser
{
    private static readonly Regex ElementRegex = new("\\belement\\s+\"(?<element>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DeclarationRegex = new("^(?:measurement|input|phasor)\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(?<expr>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex AssignmentRegex = new("^(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(?<expr>.+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex IntegrateRegex = new("^integrate\\((?<rate>.+)\\)\\s+when\\s+(?<condition>.+?)\\s+reset\\s+using\\s+setting\\(\"(?<reset>[^\"]+)\"\\)\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PersistRegex = new("^(?<source>[A-Za-z_][A-Za-z0-9_]*)\\.persist\\((?<duration>.+)\\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static AlgorithmProgram Parse(string expectedElement, string source)
    {
        var normalized = RemoveComments(source);
        var elementMatch = ElementRegex.Match(normalized);
        if (!elementMatch.Success)
            throw new AlgorithmCompilationException("A typed element declaration is required.");
        var declared = elementMatch.Groups["element"].Value;
        if (!string.Equals(declared, expectedElement, StringComparison.OrdinalIgnoreCase))
            throw new AlgorithmCompilationException($"Source declares element {declared}, but the editor selected {expectedElement}.");

        var statements = new List<Statement>();
        foreach (var logical in LogicalStatements(normalized))
        {
            var line = logical.Trim();
            if (line.Length == 0 || line.StartsWith("element ", StringComparison.OrdinalIgnoreCase) || line is "{" or "}")
                continue;

            var declaration = DeclarationRegex.Match(line);
            var assignment = declaration.Success ? declaration : AssignmentRegex.Match(line);
            if (!assignment.Success)
                throw new AlgorithmCompilationException($"Unsupported DSL statement: {line}");

            var name = assignment.Groups["name"].Value;
            var expressionText = assignment.Groups["expr"].Value.Trim();
            if (string.Equals(name, "progress", StringComparison.OrdinalIgnoreCase))
            {
                var integrate = IntegrateRegex.Match(expressionText);
                if (integrate.Success)
                {
                    statements.Add(new IntegrateStatement(
                        name,
                        ExpressionParser.Parse(integrate.Groups["rate"].Value),
                        ExpressionParser.Parse(integrate.Groups["condition"].Value),
                        integrate.Groups["reset"].Value));
                    continue;
                }
            }

            var persist = PersistRegex.Match(expressionText);
            if (persist.Success)
            {
                statements.Add(new PersistStatement(
                    name,
                    persist.Groups["source"].Value,
                    ExpressionParser.Parse(persist.Groups["duration"].Value)));
                continue;
            }

            statements.Add(new AssignmentStatement(
                name,
                ExpressionParser.Parse(expressionText),
                string.Equals(name, "trip", StringComparison.OrdinalIgnoreCase)));
        }

        if (!statements.OfType<AssignmentStatement>().Any(statement => statement.IsTrip))
            throw new AlgorithmCompilationException("A trip assignment is required.");
        return new AlgorithmProgram(expectedElement, statements);
    }

    private static IReadOnlyList<string> LogicalStatements(string source)
    {
        var result = new List<string>();
        string? current = null;
        var parentheses = 0;
        foreach (var raw in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line == "}" || line.EndsWith("{", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    result.Add(current);
                    current = null;
                    parentheses = 0;
                }
                result.Add(line);
                continue;
            }

            var continuation = current is not null &&
                (parentheses > 0 || line.StartsWith("&&", StringComparison.Ordinal) || line.StartsWith("||", StringComparison.Ordinal) ||
                 line.StartsWith("when ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("reset using ", StringComparison.OrdinalIgnoreCase));
            if (!continuation && current is not null)
            {
                result.Add(current);
                current = null;
                parentheses = 0;
            }

            current = current is null ? line : current + " " + line;
            parentheses += ParenthesisDelta(line);
        }
        if (current is not null)
            result.Add(current);
        return result;
    }

    private static int ParenthesisDelta(string value)
    {
        var delta = 0;
        var inString = false;
        var escaped = false;
        foreach (var character in value)
        {
            if (inString)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '"') inString = false;
                continue;
            }
            if (character == '"') { inString = true; continue; }
            if (character == '(') delta++;
            if (character == ')') delta--;
        }
        return delta;
    }

    private static string RemoveComments(string source)
    {
        var builder = new StringBuilder(source.Length);
        var inString = false;
        var escaped = false;
        var lineComment = false;
        var blockComment = false;
        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (lineComment)
            {
                if (current is '\r' or '\n') { lineComment = false; builder.Append(current); }
                else builder.Append(' ');
                continue;
            }
            if (blockComment)
            {
                if (current == '*' && next == '/') { builder.Append("  "); index++; blockComment = false; }
                else builder.Append(current is '\r' or '\n' ? current : ' ');
                continue;
            }
            if (inString)
            {
                builder.Append(current);
                if (escaped) escaped = false;
                else if (current == '\\') escaped = true;
                else if (current == '"') inString = false;
                continue;
            }
            if (current == '"') { inString = true; builder.Append(current); continue; }
            if (current == '/' && next == '/') { builder.Append("  "); index++; lineComment = true; continue; }
            if (current == '/' && next == '*') { builder.Append("  "); index++; blockComment = true; continue; }
            builder.Append(current);
        }
        return builder.ToString();
    }
}

internal abstract class Statement
{
    protected Statement(int instructionCost) => InstructionCost = instructionCost;
    public int InstructionCost { get; }
    public abstract void Execute(EvaluationContext context);
}

internal sealed class AssignmentStatement : Statement
{
    private readonly string _name;
    private readonly Expr _expression;

    public AssignmentStatement(string name, Expr expression, bool isTrip) : base(1 + expression.InstructionCost)
    {
        _name = name;
        _expression = expression;
        IsTrip = isTrip;
    }

    public bool IsTrip { get; }

    public override void Execute(EvaluationContext context)
    {
        context.Count(1);
        context.Set(_name, _expression.Evaluate(context));
    }
}

internal sealed class PersistStatement : Statement
{
    // One nanosecond only compensates binary floating-point accumulation at an exact
    // timer boundary (for example 12 x 5 ms). It is far below the laboratory sample
    // interval and does not advance a protection operation by a meaningful frame.
    private const double TimerBoundaryEpsilonSeconds = 1e-9;
    private readonly string _target;
    private readonly string _source;
    private readonly Expr _duration;

    public PersistStatement(string target, string source, Expr duration) : base(3 + duration.InstructionCost)
    {
        _target = target;
        _source = source;
        _duration = duration;
    }

    public override void Execute(EvaluationContext context)
    {
        context.Count(3);
        var input = context.Get(_source).AsBoolean($"{_source}.persist input");
        var durationSeconds = _duration.Evaluate(context).AsDurationSeconds("persist duration");
        if (durationSeconds < 0 || durationSeconds > TimeSpan.FromMinutes(10).TotalSeconds)
            throw new AlgorithmRuntimeException("persist duration is outside the 0..10 minute sandbox limit.");

        var timer = context.State.Timers.TryGetValue(_target, out var existing) ? existing : new TimerState(0, durationSeconds);
        if (input)
            timer = timer with { ElapsedSeconds = timer.ElapsedSeconds + context.Delta.TotalSeconds, TargetSeconds = durationSeconds };
        else if (context.TryGetBoolean("dropout", out var dropout) && dropout)
            timer = new TimerState(0, durationSeconds);
        else
            timer = timer with { TargetSeconds = durationSeconds };
        context.State.Timers[_target] = timer;
        context.Set(_target, EvalValue.Boolean(input && timer.ElapsedSeconds + TimerBoundaryEpsilonSeconds >= durationSeconds));
    }
}

internal sealed class IntegrateStatement : Statement
{
    private readonly string _target;
    private readonly Expr _rate;
    private readonly Expr _condition;
    private readonly string _resetSetting;

    public IntegrateStatement(string target, Expr rate, Expr condition, string resetSetting) : base(4 + rate.InstructionCost + condition.InstructionCost)
    {
        _target = target;
        _rate = rate;
        _condition = condition;
        _resetSetting = resetSetting;
    }

    public override void Execute(EvaluationContext context)
    {
        context.Count(4);
        var current = context.State.Integrators.TryGetValue(_target, out var existing) ? existing : 0;
        var condition = context.Frame.SmvTrust.AllowsPickup && _condition.Evaluate(context).AsBoolean("integrate condition");
        if (condition)
        {
            var increment = _rate.Evaluate(context).AsNumber("integrate rate");
            if (double.IsFinite(increment) && increment > 0)
                current += increment;
        }
        else if (context.TryGetBoolean("dropout", out var dropout) && dropout)
        {
            var reset = SettingResolver.ResetMode(_resetSetting, context.Settings);
            current = reset switch
            {
                ProtectionResetMode.Instantaneous => 0,
                ProtectionResetMode.DefiniteTime => Math.Max(0, current - context.Delta.TotalSeconds / SettingResolver.ResetDelay(context.Element, context.Settings).TotalSeconds),
                ProtectionResetMode.InverseMemory => Math.Max(0, current - context.Delta.TotalSeconds * 2),
                _ => 0
            };
        }
        context.State.Integrators[_target] = current;
        context.Set(_target, EvalValue.Number(current));
    }
}

internal sealed class RuntimeState
{
    public Dictionary<string, TimerState> Timers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> Integrators { get; } = new(StringComparer.OrdinalIgnoreCase);
    public void Clear() { Timers.Clear(); Integrators.Clear(); }
}

internal readonly record struct TimerState(double ElapsedSeconds, double TargetSeconds);

internal sealed class EvaluationContext
{
    private readonly Dictionary<string, EvalValue> _variables = new(StringComparer.OrdinalIgnoreCase);

    public EvaluationContext(string element, MeasurementFrame frame, ProtectionSettings settings, TimeSpan delta, RuntimeState state)
    {
        Element = element;
        Frame = frame;
        Settings = settings;
        Delta = delta;
        State = state;
        _variables["IA.rms1c"] = EvalValue.Number(frame.PhaseA);
        _variables["IB.rms1c"] = EvalValue.Number(frame.PhaseB);
        _variables["IC.rms1c"] = EvalValue.Number(frame.PhaseC);
        _variables["current.residual.rms1c"] = EvalValue.Number(frame.Residual);
        _variables["smv.allowsMeasurement"] = EvalValue.Boolean(frame.SmvTrust.AllowsMeasurement);
        _variables["smv.allowsPickup"] = EvalValue.Boolean(frame.SmvTrust.AllowsPickup);
        _variables["smv.allowsTrip"] = EvalValue.Boolean(frame.SmvTrust.AllowsTrip);
        _variables["dt"] = EvalValue.Duration(delta.TotalSeconds);
    }

    public string Element { get; }
    public MeasurementFrame Frame { get; }
    public ProtectionSettings Settings { get; }
    public TimeSpan Delta { get; }
    public RuntimeState State { get; }
    public int Instructions { get; private set; }

    public void Count(int amount)
    {
        Instructions += amount;
        if (Instructions > AlgorithmSandboxCompiler.MaximumInstructionsPerFrame)
            throw new AlgorithmRuntimeException("Instruction budget exceeded.");
    }

    public EvalValue Get(string name)
    {
        Count(1);
        if (_variables.TryGetValue(name, out var value))
            return value;
        throw new AlgorithmRuntimeException($"Unknown variable '{name}'.");
    }

    public void Set(string name, EvalValue value)
    {
        Count(1);
        if (!_variables.ContainsKey(name) && _variables.Count >= 64)
            throw new AlgorithmRuntimeException("Variable memory limit exceeded (64 slots).");
        _variables[name] = value;
    }

    public bool TryGetBoolean(string name, out bool value)
    {
        if (_variables.TryGetValue(name, out var raw) && raw.Kind == EvalKind.Boolean)
        {
            value = raw.BooleanValue;
            return true;
        }
        value = false;
        return false;
    }

    public bool TryGetNumber(string name, out double value)
    {
        if (_variables.TryGetValue(name, out var raw) && raw.Kind == EvalKind.Number)
        {
            value = raw.NumberValue;
            return true;
        }
        value = 0;
        return false;
    }

    public EvalValue Invoke(string function, IReadOnlyList<EvalValue> arguments)
    {
        Count(2 + arguments.Count);
        if (string.Equals(function, "max", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count < 2) throw new AlgorithmRuntimeException("max() requires at least two arguments.");
            return EvalValue.Number(arguments.Max(value => value.AsNumber("max argument")));
        }
        if (string.Equals(function, "min", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count < 2) throw new AlgorithmRuntimeException("min() requires at least two arguments.");
            return EvalValue.Number(arguments.Min(value => value.AsNumber("min argument")));
        }
        if (string.Equals(function, "abs", StringComparison.OrdinalIgnoreCase))
        {
            RequireCount(function, arguments, 1);
            return EvalValue.Number(Math.Abs(arguments[0].AsNumber("abs argument")));
        }
        if (string.Equals(function, "setting", StringComparison.OrdinalIgnoreCase))
        {
            RequireCount(function, arguments, 1);
            return SettingResolver.Resolve(arguments[0].AsText("setting name"), Settings);
        }
        if (string.Equals(function, "curve", StringComparison.OrdinalIgnoreCase))
        {
            RequireCount(function, arguments, 1);
            var curve = arguments[0].AsText("curve name");
            _ = SettingResolver.Curve(curve);
            return EvalValue.Text(curve);
        }
        if (function.EndsWith(".evaluate", StringComparison.OrdinalIgnoreCase))
        {
            RequireCount(function, arguments, 4);
            var variable = function[..^".evaluate".Length];
            var curve = Get(variable).AsText("curve characteristic");
            var family = SettingResolver.Curve(curve);
            var multiple = arguments[0].AsNumber("curve multiple");
            var tms = arguments[1].AsNumber("curve time multiplier");
            var definite = arguments[2].AsDurationSeconds("definite time");
            var minimum = arguments[3].AsDurationSeconds("minimum operate time");
            var parameters = SettingResolver.UserCurve(Element, Settings);
            var seconds = IecCurveCalculator.GetOperateTimeSeconds(
                family,
                multiple,
                tms,
                TimeSpan.FromSeconds(definite),
                TimeSpan.FromSeconds(minimum),
                parameters.K,
                parameters.Alpha,
                parameters.C);
            return EvalValue.Duration(seconds);
        }
        throw new AlgorithmRuntimeException($"Function '{function}' is not exposed by the deterministic sandbox.");
    }

    private static void RequireCount(string function, IReadOnlyList<EvalValue> arguments, int count)
    {
        if (arguments.Count != count)
            throw new AlgorithmRuntimeException($"{function}() requires {count} argument(s).");
    }
}

internal enum EvalKind { Number, Boolean, Text, Duration }

internal readonly record struct EvalValue(EvalKind Kind, double NumberValue, bool BooleanValue, string? TextValue)
{
    public static EvalValue Number(double value) => new(EvalKind.Number, value, false, null);
    public static EvalValue Boolean(bool value) => new(EvalKind.Boolean, 0, value, null);
    public static EvalValue Text(string value) => new(EvalKind.Text, 0, false, value);
    public static EvalValue Duration(double seconds) => new(EvalKind.Duration, seconds, false, null);

    public double AsNumber(string usage)
        => Kind == EvalKind.Number ? NumberValue : throw new AlgorithmRuntimeException($"{usage} requires a scalar number, not {Kind}.");
    public double AsDurationSeconds(string usage)
        => Kind == EvalKind.Duration ? NumberValue : throw new AlgorithmRuntimeException($"{usage} requires a duration value, not {Kind}.");
    public bool AsBoolean(string usage)
        => Kind == EvalKind.Boolean ? BooleanValue : throw new AlgorithmRuntimeException($"{usage} requires a boolean value, not {Kind}.");
    public string AsText(string usage)
        => Kind == EvalKind.Text && TextValue is not null ? TextValue : throw new AlgorithmRuntimeException($"{usage} requires text, not {Kind}.");
}

internal static class SettingResolver
{
    public static EvalValue Resolve(string name, ProtectionSettings settings) => name switch
    {
        "PhaseInstantaneousPickupA" => EvalValue.Number(settings.PhaseInstantaneousPickupA),
        "PhaseInstantaneousDelay" => EvalValue.Duration(settings.PhaseInstantaneousDelay.TotalSeconds),
        "PhaseInstantaneousDropoutRatio" => EvalValue.Number(settings.PhaseInstantaneousDropoutRatio),
        "PhaseTimePickupA" => EvalValue.Number(settings.PhaseTimePickupA),
        "PhaseTimeMultiplier" => EvalValue.Number(settings.PhaseTimeMultiplier),
        "PhaseTimeDefiniteDelay" => EvalValue.Duration(settings.PhaseTimeDefiniteDelay.TotalSeconds),
        "PhaseTimeMinimumOperateTime" => EvalValue.Duration(settings.PhaseTimeMinimumOperateTime.TotalSeconds),
        "PhaseTimeDropoutRatio" => EvalValue.Number(settings.PhaseTimeDropoutRatio),
        "PhaseTimeResetMode" => EvalValue.Text(settings.PhaseTimeResetMode.ToString()),
        "EarthInstantaneousPickupA" => EvalValue.Number(settings.EarthInstantaneousPickupA),
        "EarthInstantaneousDelay" => EvalValue.Duration(settings.EarthInstantaneousDelay.TotalSeconds),
        "EarthInstantaneousDropoutRatio" => EvalValue.Number(settings.EarthInstantaneousDropoutRatio),
        "EarthTimePickupA" => EvalValue.Number(settings.EarthTimePickupA),
        "EarthTimeMultiplier" => EvalValue.Number(settings.EarthTimeMultiplier),
        "EarthTimeDefiniteDelay" => EvalValue.Duration(settings.EarthTimeDefiniteDelay.TotalSeconds),
        "EarthTimeMinimumOperateTime" => EvalValue.Duration(settings.EarthTimeMinimumOperateTime.TotalSeconds),
        "EarthTimeDropoutRatio" => EvalValue.Number(settings.EarthTimeDropoutRatio),
        "EarthTimeResetMode" => EvalValue.Text(settings.EarthTimeResetMode.ToString()),
        _ => throw new AlgorithmRuntimeException($"Unknown or non-exposed setting '{name}'.")
    };

    public static bool IsEnabled(string element, ProtectionSettings settings) => element switch
    {
        "50P-1" => settings.PhaseInstantaneousEnabled,
        "51P" => settings.PhaseTimeEnabled,
        "50N" => settings.EarthInstantaneousEnabled,
        "51N" => settings.EarthTimeEnabled,
        _ => false
    };

    public static double Pickup(string element, ProtectionSettings settings) => element switch
    {
        "50P-1" => settings.PhaseInstantaneousPickupA,
        "51P" => settings.PhaseTimePickupA,
        "50N" => settings.EarthInstantaneousPickupA,
        "51N" => settings.EarthTimePickupA,
        _ => 0
    };

    public static TimeSpan ResetDelay(string element, ProtectionSettings settings) => element switch
    {
        "51P" => settings.PhaseTimeResetDelay,
        "51N" => settings.EarthTimeResetDelay,
        _ => TimeSpan.FromSeconds(1)
    };

    public static ProtectionResetMode ResetMode(string settingName, ProtectionSettings settings)
        => Resolve(settingName, settings).AsText("reset mode") switch
        {
            nameof(ProtectionResetMode.Instantaneous) => ProtectionResetMode.Instantaneous,
            nameof(ProtectionResetMode.DefiniteTime) => ProtectionResetMode.DefiniteTime,
            nameof(ProtectionResetMode.InverseMemory) => ProtectionResetMode.InverseMemory,
            var value => throw new AlgorithmRuntimeException($"Unsupported reset mode '{value}'.")
        };

    public static IecCurveFamily Curve(string value)
        => Enum.TryParse<IecCurveFamily>(value, ignoreCase: true, out var family) && Enum.IsDefined(family)
            ? family
            : throw new AlgorithmRuntimeException($"Unknown IEC curve '{value}'.");

    public static (double K, double Alpha, double C) UserCurve(string element, ProtectionSettings settings)
        => element == "51N"
            ? (settings.EarthTimeUserK, settings.EarthTimeUserAlpha, settings.EarthTimeUserC)
            : (settings.PhaseTimeUserK, settings.PhaseTimeUserAlpha, settings.PhaseTimeUserC);
}

internal static class ElementIds
{
    public static ProtectionElementId Resolve(string element) => element switch
    {
        "50P-1" => ProtectionElementId.PhaseInstantaneous50P,
        "51P" => ProtectionElementId.PhaseTime51P,
        "50N" => ProtectionElementId.EarthInstantaneous50N,
        "51N" => ProtectionElementId.EarthTime51N,
        _ => throw new AlgorithmRuntimeException($"Unsupported runtime element '{element}'.")
    };
}

internal abstract class Expr
{
    protected Expr(int instructionCost) => InstructionCost = instructionCost;
    public int InstructionCost { get; }
    public abstract EvalValue Evaluate(EvaluationContext context);
}

internal sealed class LiteralExpr : Expr
{
    private readonly EvalValue _value;
    public LiteralExpr(EvalValue value) : base(1) => _value = value;
    public override EvalValue Evaluate(EvaluationContext context) { context.Count(1); return _value; }
}

internal sealed class VariableExpr : Expr
{
    private readonly string _name;
    public VariableExpr(string name) : base(1) => _name = name;
    public override EvalValue Evaluate(EvaluationContext context) => context.Get(_name);
}

internal sealed class UnaryExpr : Expr
{
    private readonly TokenKind _operator;
    private readonly Expr _operand;
    public UnaryExpr(TokenKind @operator, Expr operand) : base(1 + operand.InstructionCost) { _operator = @operator; _operand = operand; }
    public override EvalValue Evaluate(EvaluationContext context)
    {
        context.Count(1);
        var value = _operand.Evaluate(context);
        return _operator switch
        {
            TokenKind.Bang => EvalValue.Boolean(!value.AsBoolean("logical negation")),
            TokenKind.Minus => EvalValue.Number(-value.AsNumber("numeric negation")),
            TokenKind.Plus => EvalValue.Number(value.AsNumber("unary plus")),
            _ => throw new AlgorithmRuntimeException("Unsupported unary operator.")
        };
    }
}

internal sealed class BinaryExpr : Expr
{
    private readonly Expr _left;
    private readonly TokenKind _operator;
    private readonly Expr _right;
    public BinaryExpr(Expr left, TokenKind @operator, Expr right) : base(1 + left.InstructionCost + right.InstructionCost) { _left = left; _operator = @operator; _right = right; }
    public override EvalValue Evaluate(EvaluationContext context)
    {
        context.Count(1);
        if (_operator == TokenKind.AndAnd)
        {
            var left = _left.Evaluate(context).AsBoolean("&& left operand");
            return EvalValue.Boolean(left && _right.Evaluate(context).AsBoolean("&& right operand"));
        }
        if (_operator == TokenKind.OrOr)
        {
            var left = _left.Evaluate(context).AsBoolean("|| left operand");
            return EvalValue.Boolean(left || _right.Evaluate(context).AsBoolean("|| right operand"));
        }

        var lhs = _left.Evaluate(context);
        var rhs = _right.Evaluate(context);
        return _operator switch
        {
            TokenKind.Plus => Add(lhs, rhs),
            TokenKind.Minus => Subtract(lhs, rhs),
            TokenKind.Star => Multiply(lhs, rhs),
            TokenKind.Slash => Divide(lhs, rhs),
            TokenKind.Greater => EvalValue.Boolean(Comparable(lhs, rhs, ">") > 0),
            TokenKind.GreaterEqual => EvalValue.Boolean(Comparable(lhs, rhs, ">=") >= 0),
            TokenKind.Less => EvalValue.Boolean(Comparable(lhs, rhs, "<") < 0),
            TokenKind.LessEqual => EvalValue.Boolean(Comparable(lhs, rhs, "<=") <= 0),
            TokenKind.EqualEqual => EvalValue.Boolean(Equal(lhs, rhs)),
            TokenKind.BangEqual => EvalValue.Boolean(!Equal(lhs, rhs)),
            _ => throw new AlgorithmRuntimeException("Unsupported binary operator.")
        };
    }

    private static EvalValue Add(EvalValue left, EvalValue right)
    {
        if (left.Kind == EvalKind.Duration && right.Kind == EvalKind.Duration) return EvalValue.Duration(left.NumberValue + right.NumberValue);
        return EvalValue.Number(left.AsNumber("+ left operand") + right.AsNumber("+ right operand"));
    }
    private static EvalValue Subtract(EvalValue left, EvalValue right)
    {
        if (left.Kind == EvalKind.Duration && right.Kind == EvalKind.Duration) return EvalValue.Duration(left.NumberValue - right.NumberValue);
        return EvalValue.Number(left.AsNumber("- left operand") - right.AsNumber("- right operand"));
    }
    private static EvalValue Multiply(EvalValue left, EvalValue right)
    {
        if (left.Kind == EvalKind.Duration && right.Kind == EvalKind.Number) return EvalValue.Duration(left.NumberValue * right.NumberValue);
        if (left.Kind == EvalKind.Number && right.Kind == EvalKind.Duration) return EvalValue.Duration(left.NumberValue * right.NumberValue);
        return EvalValue.Number(left.AsNumber("* left operand") * right.AsNumber("* right operand"));
    }
    private static EvalValue Divide(EvalValue left, EvalValue right)
    {
        if (Math.Abs(right.NumberValue) < 1e-15) return EvalValue.Number(0);
        if (left.Kind == EvalKind.Duration && right.Kind == EvalKind.Duration) return EvalValue.Number(left.NumberValue / right.NumberValue);
        if (left.Kind == EvalKind.Duration && right.Kind == EvalKind.Number) return EvalValue.Duration(left.NumberValue / right.NumberValue);
        return EvalValue.Number(left.AsNumber("/ left operand") / right.AsNumber("/ right operand"));
    }
    private static int Comparable(EvalValue left, EvalValue right, string usage)
    {
        if (left.Kind != right.Kind || left.Kind is not (EvalKind.Number or EvalKind.Duration))
            throw new AlgorithmRuntimeException($"{usage} requires matching numeric or duration operands.");
        return left.NumberValue.CompareTo(right.NumberValue);
    }
    private static bool Equal(EvalValue left, EvalValue right)
    {
        if (left.Kind != right.Kind) return false;
        return left.Kind switch
        {
            EvalKind.Boolean => left.BooleanValue == right.BooleanValue,
            EvalKind.Text => string.Equals(left.TextValue, right.TextValue, StringComparison.OrdinalIgnoreCase),
            _ => Math.Abs(left.NumberValue - right.NumberValue) < 1e-12
        };
    }
}

internal sealed class CallExpr : Expr
{
    private readonly string _function;
    private readonly IReadOnlyList<Expr> _arguments;
    public CallExpr(string function, IReadOnlyList<Expr> arguments) : base(2 + arguments.Sum(argument => argument.InstructionCost)) { _function = function; _arguments = arguments; }
    public override EvalValue Evaluate(EvaluationContext context)
    {
        var values = _arguments.Select(argument => argument.Evaluate(context)).ToArray();
        return context.Invoke(_function, values);
    }
}

internal enum TokenKind
{
    End, Number, Identifier, String, True, False,
    LeftParen, RightParen, Comma,
    Plus, Minus, Star, Slash, Bang,
    Greater, GreaterEqual, Less, LessEqual, EqualEqual, BangEqual,
    AndAnd, OrOr
}

internal readonly record struct Token(TokenKind Kind, string Text, double Number = 0);

internal static class ExpressionParser
{
    public static Expr Parse(string text)
    {
        var parser = new Parser(text);
        var expression = parser.ParseExpression();
        parser.Expect(TokenKind.End);
        return expression;
    }

    private sealed class Parser
    {
        private readonly Lexer _lexer;
        private Token _current;
        public Parser(string text) { _lexer = new Lexer(text); _current = _lexer.Next(); }
        public Expr ParseExpression() => ParseOr();
        public void Expect(TokenKind kind)
        {
            if (_current.Kind != kind) throw new AlgorithmCompilationException($"Expected {kind}, found '{_current.Text}'.");
            _current = _lexer.Next();
        }
        private Expr ParseOr()
        {
            var left = ParseAnd();
            while (_current.Kind == TokenKind.OrOr) { var op = _current.Kind; Next(); left = new BinaryExpr(left, op, ParseAnd()); }
            return left;
        }
        private Expr ParseAnd()
        {
            var left = ParseEquality();
            while (_current.Kind == TokenKind.AndAnd) { var op = _current.Kind; Next(); left = new BinaryExpr(left, op, ParseEquality()); }
            return left;
        }
        private Expr ParseEquality()
        {
            var left = ParseComparison();
            while (_current.Kind is TokenKind.EqualEqual or TokenKind.BangEqual) { var op = _current.Kind; Next(); left = new BinaryExpr(left, op, ParseComparison()); }
            return left;
        }
        private Expr ParseComparison()
        {
            var left = ParseTerm();
            while (_current.Kind is TokenKind.Greater or TokenKind.GreaterEqual or TokenKind.Less or TokenKind.LessEqual) { var op = _current.Kind; Next(); left = new BinaryExpr(left, op, ParseTerm()); }
            return left;
        }
        private Expr ParseTerm()
        {
            var left = ParseFactor();
            while (_current.Kind is TokenKind.Plus or TokenKind.Minus) { var op = _current.Kind; Next(); left = new BinaryExpr(left, op, ParseFactor()); }
            return left;
        }
        private Expr ParseFactor()
        {
            var left = ParseUnary();
            while (_current.Kind is TokenKind.Star or TokenKind.Slash) { var op = _current.Kind; Next(); left = new BinaryExpr(left, op, ParseUnary()); }
            return left;
        }
        private Expr ParseUnary()
        {
            if (_current.Kind is TokenKind.Bang or TokenKind.Minus or TokenKind.Plus) { var op = _current.Kind; Next(); return new UnaryExpr(op, ParseUnary()); }
            return ParsePrimary();
        }
        private Expr ParsePrimary()
        {
            if (_current.Kind == TokenKind.Number) { var value = _current.Number; Next(); return new LiteralExpr(EvalValue.Number(value)); }
            if (_current.Kind == TokenKind.String) { var value = _current.Text; Next(); return new LiteralExpr(EvalValue.Text(value)); }
            if (_current.Kind == TokenKind.True) { Next(); return new LiteralExpr(EvalValue.Boolean(true)); }
            if (_current.Kind == TokenKind.False) { Next(); return new LiteralExpr(EvalValue.Boolean(false)); }
            if (_current.Kind == TokenKind.Identifier)
            {
                var name = _current.Text;
                Next();
                if (_current.Kind != TokenKind.LeftParen) return new VariableExpr(name);
                Next();
                var arguments = new List<Expr>();
                if (_current.Kind != TokenKind.RightParen)
                {
                    while (true)
                    {
                        arguments.Add(ParseExpression());
                        if (_current.Kind != TokenKind.Comma) break;
                        Next();
                    }
                }
                Expect(TokenKind.RightParen);
                return new CallExpr(name, arguments);
            }
            if (_current.Kind == TokenKind.LeftParen)
            {
                Next();
                var expression = ParseExpression();
                Expect(TokenKind.RightParen);
                return expression;
            }
            throw new AlgorithmCompilationException($"Unexpected token '{_current.Text}' in expression.");
        }
        private void Next() => _current = _lexer.Next();
    }

    private sealed class Lexer
    {
        private readonly string _text;
        private int _index;
        public Lexer(string text) => _text = text;
        public Token Next()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
            if (_index >= _text.Length) return new Token(TokenKind.End, string.Empty);
            var ch = _text[_index];
            if (char.IsDigit(ch) || ch == '.')
            {
                var start = _index++;
                while (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    if ((_text[_index] is '+' or '-') && _text[_index - 1] is not ('e' or 'E')) break;
                    _index++;
                }
                var raw = _text[start.._index];
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    throw new AlgorithmCompilationException($"Invalid number '{raw}'.");
                return new Token(TokenKind.Number, raw, number);
            }
            if (char.IsLetter(ch) || ch == '_')
            {
                var start = _index++;
                while (_index < _text.Length && (char.IsLetterOrDigit(_text[_index]) || _text[_index] is '_' or '.')) _index++;
                var value = _text[start.._index];
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return new Token(TokenKind.True, value);
                if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return new Token(TokenKind.False, value);
                return new Token(TokenKind.Identifier, value);
            }
            if (ch == '"')
            {
                _index++;
                var builder = new StringBuilder();
                var escaped = false;
                while (_index < _text.Length)
                {
                    var current = _text[_index++];
                    if (escaped) { builder.Append(current); escaped = false; continue; }
                    if (current == '\\') { escaped = true; continue; }
                    if (current == '"') return new Token(TokenKind.String, builder.ToString());
                    builder.Append(current);
                }
                throw new AlgorithmCompilationException("Unterminated string literal.");
            }
            _index++;
            return ch switch
            {
                '(' => new Token(TokenKind.LeftParen, "("),
                ')' => new Token(TokenKind.RightParen, ")"),
                ',' => new Token(TokenKind.Comma, ","),
                '+' => new Token(TokenKind.Plus, "+"),
                '-' => new Token(TokenKind.Minus, "-"),
                '*' => new Token(TokenKind.Star, "*"),
                '/' => new Token(TokenKind.Slash, "/"),
                '!' when Match('=') => new Token(TokenKind.BangEqual, "!="),
                '!' => new Token(TokenKind.Bang, "!"),
                '>' when Match('=') => new Token(TokenKind.GreaterEqual, ">="),
                '>' => new Token(TokenKind.Greater, ">"),
                '<' when Match('=') => new Token(TokenKind.LessEqual, "<="),
                '<' => new Token(TokenKind.Less, "<"),
                '=' when Match('=') => new Token(TokenKind.EqualEqual, "=="),
                '&' when Match('&') => new Token(TokenKind.AndAnd, "&&"),
                '|' when Match('|') => new Token(TokenKind.OrOr, "||"),
                _ => throw new AlgorithmCompilationException($"Unsupported character '{ch}' in expression.")
            };
        }
        private bool Match(char expected)
        {
            if (_index >= _text.Length || _text[_index] != expected) return false;
            _index++;
            return true;
        }
    }
}
