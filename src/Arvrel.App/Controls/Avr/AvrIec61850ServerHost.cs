using System.Collections.Concurrent;
using System.Globalization;
using Arvrel.Application.Ied;

#if ARIEC61850_SIBLING
using AR.Iec61850.Simulation;
#endif

namespace Arvrel.App.Controls.Avr;

internal sealed record AvrIec61850ServerStatus
{
    public bool EngineAvailable { get; init; }
    public bool IsRunning { get; init; }
    public string Host { get; init; } = "0.0.0.0";
    public int Port { get; init; } = 102;
    public int ActiveConnections { get; init; }
    public long AcceptedConnections { get; init; }
    public long ServedRequests { get; init; }
    public long ReportsSent { get; init; }
    public long RejectedWrites { get; init; }
    public long AcceptedControls { get; init; }
    public long RejectedControls { get; init; }
    public string LastControl { get; init; } = "No remote control received";
    public string LastActivity { get; init; } = "Server stopped";

    public string Endpoint => $"{Host}:{Port}";
}

/// <summary>
/// Bridges the AVR runtime into the sibling ARIEC61850 simulator/MMS server.
/// The network stack remains owned by ARIEC61850; this class defines the AVR
/// logical model, mirrors live AVR values and queues accepted IEC controls back
/// to the WPF/AVR authority thread.
/// </summary>
internal sealed class AvrIec61850ServerHost : IAsyncDisposable
{
    private readonly ConcurrentQueue<AvrIec61850Command> _commands = new();

#if ARIEC61850_SIBLING
    private const string LogicalDevice = "ARVAVR1";
    private readonly object _gate = new();
    private IedSimulatorEngine? _engine;
    private IedSimulatorMmsServer? _server;
    private string _host = "0.0.0.0";
    private int _port = 102;
    private string _lastActivity = "Server stopped";
    private string _lastControl = "No remote control received";
    private long _acceptedControls;
    private long _rejectedControls;
    private AvrSnapshot _latestSnapshot = AvrSnapshot.Ready(new AvrSettings(), 0);
    private AvrSettings _latestSettings = new();
    private bool _latestRemoteBlock;

    public bool EngineAvailable => true;

    public void Start(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("IEC 61850 bind address is required.", nameof(host));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "IEC 61850 TCP port must be between 1 and 65535.");

        lock (_gate)
        {
            if (_server?.IsRunning == true)
                throw new InvalidOperationException("The AVR IEC 61850 server is already running.");

            while (_commands.TryDequeue(out _)) { }
            Interlocked.Exchange(ref _acceptedControls, 0);
            Interlocked.Exchange(ref _rejectedControls, 0);
            _lastControl = "No remote control received";

            var profile = CreateAvrProfile();
            var engine = new IedSimulatorEngine(profile);
            var modelBuilder = new MmsReadOnlyServerModelBuilder();
            var serverOptions = new IedSimulatorMmsServerOptions
            {
                Host = host.Trim(),
                Port = port,
                ServerName = "ARVREL AVR-230 Virtual IED",
                AssociationRuntimeFactory = remote => new AvrIec61850ControlRuntime(
                    remote,
                    GetControlContext,
                    EnqueueCommand,
                    AuditControl)
            };
            var profileOptions = new MmsReadOnlyServerProfileOptions
            {
                ServerName = serverOptions.ServerName,
                Port = port,
                IncludeSelfTest = false
            };

            MmsReadOnlyServerSession SessionFactory()
            {
                lock (_gate)
                {
                    var snapshot = engine.CreateSnapshot(DateTimeOffset.UtcNow);
                    var model = modelBuilder.Build(profile, snapshot, profileOptions);
                    return new MmsReadOnlyServerSession(model);
                }
            }

            var server = new IedSimulatorMmsServer(SessionFactory, serverOptions);
            server.Activity += Server_Activity;
            try
            {
                server.Start();
            }
            catch
            {
                server.Activity -= Server_Activity;
                throw;
            }

            _engine = engine;
            _server = server;
            _host = host.Trim();
            _port = server.BoundPort;
            _lastActivity = $"Listening on {_host}:{_port}";
        }
    }

    public async Task StopAsync()
    {
        IedSimulatorMmsServer? server;
        lock (_gate)
        {
            server = _server;
            _server = null;
            _engine = null;
            _lastActivity = "Server stopping";
        }

        while (_commands.TryDequeue(out _)) { }
        if (server is null)
            return;

        server.Activity -= Server_Activity;
        await server.StopAsync().ConfigureAwait(false);
        await server.DisposeAsync().ConfigureAwait(false);

        lock (_gate)
            _lastActivity = "Server stopped";
    }

    public void Publish(AvrSnapshot snapshot, AvrSettings settings, double frequencyHz, bool remoteBlock)
    {
        lock (_gate)
        {
            _latestSnapshot = snapshot;
            _latestSettings = settings;
            _latestRemoteBlock = remoteBlock;
            if (_engine is null)
                return;

            var now = DateTimeOffset.UtcNow;
            SetMeasurement("ATCC1.CtlV.mag.f", snapshot.MeasuredVoltageV, now);
            SetMeasurement("ATCC1.LodA.mag.f", snapshot.SourceCurrentA, now);
            SetMeasurement("ATCC1.BndCtr.setMag.f", settings.SetpointVoltageV, now);
            SetMeasurement("ATCC1.BndWid.setMag.f", settings.SetpointVoltageV * settings.TolerancePercent * 2.0 / 100.0, now);
            SetInteger("ATCC1.CtlDlTmms.setVal", (int)Math.Round(settings.T1Seconds * 1000.0), now);
            SetMeasurement("ATCC1.BlkVLo.setMag.f", settings.NominalVoltageV * settings.UndervoltageBlockPercent / 100.0, now);
            SetMeasurement("ATCC1.BlkVHi.setMag.f", settings.NominalVoltageV * settings.OvervoltageBlockPercent / 100.0, now);
            SetStatus("ATCC1.Loc.stVal", snapshot.Authority == AvrControlAuthority.Local, now);
            SetStatus("ATCC1.Auto.stVal", snapshot.Mode == AvrOperatingMode.Automatic, now);
            SetStatus("ATCC1.LTCBlk.stVal", remoteBlock, now);
            SetStatus("ATCC1.TapOpR.stVal", snapshot.RaiseOutput, now);
            SetStatus("ATCC1.TapOpL.stVal", snapshot.LowerOutput, now);
            SetText("ATCC1.Beh.stVal", snapshot.Blocked ? "blocked" : "on", now);

            SetInteger("YLTC1.TapPos.stVal", snapshot.TapPosition, now);
            SetInteger("YLTC1.TapChg.valWTr.posVal", snapshot.RaiseOutput ? 2 : snapshot.LowerOutput ? 1 : 0, now);
            SetStatus("YLTC1.TapChg.valWTr.transInd", snapshot.TapMoving, now);
            SetStatus("YLTC1.EndPosR.stVal", snapshot.TapPosition >= settings.MaximumTap, now);
            SetStatus("YLTC1.EndPosL.stVal", snapshot.TapPosition <= settings.MinimumTap, now);
            SetInteger("YLTC1.OpCnt.stVal", snapshot.OperationCount, now);

            SetMeasurement("MMXU1.Vol.mag.f", snapshot.MeasuredVoltageV, now);
            SetMeasurement("MMXU1.A.mag.f", snapshot.SourceCurrentA, now);
            SetMeasurement("MMXU1.Hz.mag.f", snapshot.SourceEnergized ? frequencyHz : 0.0, now);
            SetMeasurement("MMXU1.PF.mag.f", snapshot.SourceEnergized ? snapshot.PowerFactor : 0.0, now);
            SetStatus("GGIO1.SourceOn.stVal", snapshot.SourceEnergized, now);
            SetStatus("GGIO1.TapMoving.stVal", snapshot.TapMoving, now);
        }
    }

    public bool TryDequeueCommand(out AvrIec61850Command command)
        => _commands.TryDequeue(out command!);

    public AvrIec61850ServerStatus GetStatus()
    {
        lock (_gate)
        {
            var server = _server;
            var activity = server?.RecentActivity() ?? Array.Empty<IedSimulatorServerActivity>();
            return new AvrIec61850ServerStatus
            {
                EngineAvailable = true,
                IsRunning = server?.IsRunning == true,
                Host = _host,
                Port = server?.BoundPort > 0 ? server.BoundPort : _port,
                ActiveConnections = server?.ActiveConnectionCount ?? 0,
                AcceptedConnections = server?.AcceptedConnectionCount ?? 0,
                ServedRequests = server?.ServedRequestCount ?? 0,
                ReportsSent = activity.LongCount(x => x.Kind == IedSimulatorServerActivityKind.ReportSent),
                RejectedWrites = server?.RejectedWriteCount ?? 0,
                AcceptedControls = Interlocked.Read(ref _acceptedControls),
                RejectedControls = Interlocked.Read(ref _rejectedControls),
                LastControl = _lastControl,
                LastActivity = _lastActivity
            };
        }
    }

    private AvrIec61850ControlContext GetControlContext()
    {
        lock (_gate)
        {
            var width = _latestSettings.SetpointVoltageV * _latestSettings.TolerancePercent * 2.0 / 100.0;
            return new AvrIec61850ControlContext(
                _latestSnapshot.Authority,
                _latestSnapshot.Mode,
                _latestSnapshot.TapPosition,
                _latestSettings.MinimumTap,
                _latestSettings.MaximumTap,
                _latestSnapshot.TapMoving,
                _latestRemoteBlock,
                _latestSettings.NominalVoltageV,
                _latestSettings.SetpointVoltageV,
                width);
        }
    }

    private void EnqueueCommand(AvrIec61850Command command) => _commands.Enqueue(command);

    private void AuditControl(bool accepted, string target, string message)
    {
        if (accepted)
            Interlocked.Increment(ref _acceptedControls);
        else
            Interlocked.Increment(ref _rejectedControls);
        lock (_gate)
            _lastControl = $"{DateTime.Now:HH:mm:ss} {(accepted ? "ACCEPT" : "REJECT")} {target} · {message}";
    }

    private void Server_Activity(object? sender, IedSimulatorServerActivity activity)
    {
        lock (_gate)
            _lastActivity = activity.Summary;
    }

    private void SetMeasurement(string reference, double value, DateTimeOffset timestamp)
        => SetText(reference, value.ToString("0.###", CultureInfo.InvariantCulture), timestamp);

    private void SetInteger(string reference, int value, DateTimeOffset timestamp)
        => SetText(reference, value.ToString(CultureInfo.InvariantCulture), timestamp);

    private void SetStatus(string reference, bool value, DateTimeOffset timestamp)
        => SetText(reference, value ? "true" : "false", timestamp);

    private void SetText(string reference, string value, DateTimeOffset timestamp)
    {
        if (_engine is null || !_engine.TryGetPointState(reference, out var state))
            return;

        var changed = !string.Equals(state.Value, value, StringComparison.Ordinal);
        state.Value = value;
        state.Quality = "valid";
        state.TimestampUtc = timestamp;
        state.Reason = changed ? "data-change" : "sample";
    }

    private static IedSimulatorProfile CreateAvrProfile()
    {
        var atccPoints = new List<IedSimulatorPoint>
        {
            FixedMeasurement("ATCC1.CtlV.mag.f", "MX", "V"),
            FixedMeasurement("ATCC1.LodA.mag.f", "MX", "A"),
            FixedMeasurement("ATCC1.BndCtr.setMag.f", "SP", "V"),
            FixedMeasurement("ATCC1.BndWid.setMag.f", "SP", "V"),
            FixedInteger("ATCC1.CtlDlTmms.setVal", "SP", "INT32U", "10000"),
            FixedMeasurement("ATCC1.BlkVLo.setMag.f", "SP", "V"),
            FixedMeasurement("ATCC1.BlkVHi.setMag.f", "SP", "V"),
            BooleanStatus("ATCC1.Loc.stVal"),
            BooleanStatus("ATCC1.Auto.stVal"),
            BooleanStatus("ATCC1.LTCBlk.stVal"),
            BooleanStatus("ATCC1.TapOpR.stVal"),
            BooleanStatus("ATCC1.TapOpL.stVal"),
            TextStatus("ATCC1.Beh.stVal", "on")
        };
        atccPoints.AddRange(ControlObjectPoints("ATCC1.Auto", "BOOLEAN"));
        atccPoints.AddRange(ControlObjectPoints("ATCC1.LTCBlk", "BOOLEAN"));

        var yltcPoints = new List<IedSimulatorPoint>
        {
            IntegerStatus("YLTC1.TapPos.stVal"),
            FixedInteger("YLTC1.TapChg.valWTr.posVal", "ST", "TCMD", "0"),
            FixedBoolean("YLTC1.TapChg.valWTr.transInd", "ST", false),
            BooleanStatus("YLTC1.EndPosR.stVal"),
            BooleanStatus("YLTC1.EndPosL.stVal"),
            IntegerStatus("YLTC1.OpCnt.stVal")
        };
        yltcPoints.AddRange(ControlObjectPoints("YLTC1.TapChg", "INT8"));

        var mmxuPoints = new IedSimulatorPoint[]
        {
            FixedMeasurement("MMXU1.Vol.mag.f", "MX", "V"),
            FixedMeasurement("MMXU1.A.mag.f", "MX", "A"),
            FixedMeasurement("MMXU1.Hz.mag.f", "MX", "Hz"),
            FixedMeasurement("MMXU1.PF.mag.f", "MX", string.Empty)
        };

        var ggioPoints = new IedSimulatorPoint[]
        {
            BooleanStatus("GGIO1.SourceOn.stVal"),
            BooleanStatus("GGIO1.TapMoving.stVal")
        };

        var allMeasurements = atccPoints.Where(x => x.FunctionalConstraint == "MX")
            .Concat(mmxuPoints)
            .Select(x => $"{LogicalDevice}/{x.Reference}")
            .ToArray();
        var allSettings = atccPoints.Where(x => x.FunctionalConstraint == "SP")
            .Select(x => $"{LogicalDevice}/{x.Reference}")
            .ToArray();
        var allStatus = atccPoints.Where(x => x.FunctionalConstraint == "ST")
            .Concat(yltcPoints.Where(x => x.FunctionalConstraint == "ST"))
            .Concat(ggioPoints)
            .Select(x => $"{LogicalDevice}/{x.Reference}")
            .ToArray();

        return new IedSimulatorProfile
        {
            Name = "ARVREL AVR-230",
            Vendor = "ARVREL",
            Edition = "IEC 61850 Ed2-style AVR lab profile",
            LogicalDevices = new[]
            {
                new IedSimulatorLogicalDevice
                {
                    Name = LogicalDevice,
                    LogicalNodes = new IedSimulatorLogicalNode[]
                    {
                        new() { Name = "LLN0", LnClass = "LLN0", Points = Array.Empty<IedSimulatorPoint>() },
                        new() { Name = "LPHD1", LnClass = "LPHD", Points = Array.Empty<IedSimulatorPoint>() },
                        new() { Name = "ATCC1", LnClass = "ATCC", Points = atccPoints.ToArray() },
                        new() { Name = "YLTC1", LnClass = "YLTC", Points = yltcPoints.ToArray() },
                        new() { Name = "MMXU1", LnClass = "MMXU", Points = mmxuPoints },
                        new() { Name = "GGIO1", LnClass = "GGIO", Points = ggioPoints }
                    }
                }
            },
            DataSets = new IedSimulatorDataSet[]
            {
                new() { Reference = $"{LogicalDevice}/LLN0.dsMeas", Members = allMeasurements },
                new() { Reference = $"{LogicalDevice}/LLN0.dsStatus", Members = allStatus },
                new() { Reference = $"{LogicalDevice}/LLN0.dsSettings", Members = allSettings }
            },
            ReportControlBlocks = new IedSimulatorReportControlBlock[]
            {
                new()
                {
                    Reference = $"{LogicalDevice}/LLN0.BR.rptMeas01",
                    Buffered = true,
                    DataSetReference = $"{LogicalDevice}/LLN0.dsMeas",
                    ReportId = "ARV_AVR_MEAS_01",
                    ConfRev = 2,
                    BufferTimeMs = 100,
                    IntegrityPeriodMs = 1000,
                    TriggerOptions = "data-change, integrity, GI",
                    OptionalFields = "seqNum, entryId, timeStamp, reasonCode, dataSet, confRev"
                },
                new()
                {
                    Reference = $"{LogicalDevice}/LLN0.RP.rptStatus01",
                    Buffered = false,
                    DataSetReference = $"{LogicalDevice}/LLN0.dsStatus",
                    ReportId = "ARV_AVR_STATUS_01",
                    ConfRev = 2,
                    BufferTimeMs = 0,
                    IntegrityPeriodMs = 1000,
                    TriggerOptions = "data-change, integrity, GI",
                    OptionalFields = "seqNum, timeStamp, reasonCode, dataSet, confRev"
                }
            }
        };
    }

    private static IEnumerable<IedSimulatorPoint> ControlObjectPoints(string root, string controlType)
    {
        yield return FixedText($"{root}.SBO", "CO", "VISSTRING129", string.Empty);
        foreach (var service in new[] { "SBOw", "Oper", "Cancel" })
        {
            yield return FixedText($"{root}.{service}.ctlVal", "CO", controlType, controlType == "BOOLEAN" ? "false" : "0");
            yield return FixedInteger($"{root}.{service}.origin.orCat", "CO", "INT8", "0");
            yield return FixedText($"{root}.{service}.origin.orIdent", "CO", "OCTET64", string.Empty);
            yield return FixedInteger($"{root}.{service}.ctlNum", "CO", "INT8U", "0");
            yield return FixedText($"{root}.{service}.T", "CO", "TIMESTAMP", string.Empty);
            yield return FixedBoolean($"{root}.{service}.Test", "CO", false);
            yield return FixedText($"{root}.{service}.Check", "CO", "CHECK", string.Empty);
        }

        yield return FixedInteger($"{root}.ctlModel", "CF", "ENUM", "4");
        yield return FixedInteger($"{root}.sboTimeout", "CF", "INT32U", "5000");
        yield return FixedInteger($"{root}.sboClass", "CF", "ENUM", "0");
    }

    private static IedSimulatorPoint FixedMeasurement(string reference, string functionalConstraint, string unit)
        => IedSimulatorPoint.Measurement(reference, functionalConstraint, unit, 0, 0, 0, isDynamic: false, sclBType: "FLOAT32");

    private static IedSimulatorPoint FixedInteger(string reference, string functionalConstraint, string sclType, string value)
        => new()
        {
            Reference = reference,
            FunctionalConstraint = functionalConstraint,
            Kind = "status",
            SclBType = sclType,
            InitialValue = value
        };

    private static IedSimulatorPoint FixedBoolean(string reference, string functionalConstraint, bool value)
        => new()
        {
            Reference = reference,
            FunctionalConstraint = functionalConstraint,
            Kind = "status",
            SclBType = "BOOLEAN",
            InitialValue = value ? "true" : "false"
        };

    private static IedSimulatorPoint FixedText(string reference, string functionalConstraint, string sclType, string value)
        => new()
        {
            Reference = reference,
            FunctionalConstraint = functionalConstraint,
            Kind = "status",
            SclBType = sclType,
            InitialValue = value
        };

    private static IedSimulatorPoint BooleanStatus(string reference)
        => FixedBoolean(reference, "ST", false);

    private static IedSimulatorPoint IntegerStatus(string reference)
        => FixedInteger(reference, "ST", "INT32", "0");

    private static IedSimulatorPoint TextStatus(string reference, string initialValue)
        => new()
        {
            Reference = reference,
            FunctionalConstraint = "ST",
            Kind = "status",
            InitialValue = initialValue
        };
#else
    public bool EngineAvailable => false;

    public void Start(string host, int port)
        => throw new InvalidOperationException("ARIEC61850 sibling simulation engine is not available in this build.");

    public Task StopAsync() => Task.CompletedTask;

    public void Publish(AvrSnapshot snapshot, AvrSettings settings, double frequencyHz, bool remoteBlock)
    {
    }

    public bool TryDequeueCommand(out AvrIec61850Command command)
    {
        command = null!;
        return false;
    }

    public AvrIec61850ServerStatus GetStatus()
        => new()
        {
            EngineAvailable = false,
            IsRunning = false,
            LastActivity = "ARIEC61850 sibling simulation engine unavailable"
        };
#endif

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
