namespace Arvrel.Application.Laboratory;

public sealed record MetrologyTimingProfile(
    int ClockResolutionMicroseconds,
    int BinaryInputSampleRateHz,
    TimeSpan BinaryInputDeglitch,
    TimeSpan BinaryInputDebounceHoldoff)
{
    public static MetrologyTimingProfile CmcStyle { get; } = new(
        ClockResolutionMicroseconds: 1,
        BinaryInputSampleRateHz: 10_000,
        BinaryInputDeglitch: TimeSpan.FromMilliseconds(0.5),
        BinaryInputDebounceHoldoff: TimeSpan.Zero);

    public int BinaryInputSamplePeriodMicroseconds => 1_000_000 / BinaryInputSampleRateHz;

    public void Validate()
    {
        if (ClockResolutionMicroseconds <= 0 || ClockResolutionMicroseconds > 1_000)
            throw new ArgumentOutOfRangeException(nameof(ClockResolutionMicroseconds));
        if (1_000_000 % ClockResolutionMicroseconds != 0)
            throw new ArgumentOutOfRangeException(nameof(ClockResolutionMicroseconds), "Clock resolution must divide one second exactly.");
        if (BinaryInputSampleRateHz <= 0 || BinaryInputSampleRateHz > 1_000_000 || 1_000_000 % BinaryInputSampleRateHz != 0)
            throw new ArgumentOutOfRangeException(nameof(BinaryInputSampleRateHz), "BI sample rate must divide one second exactly.");
        ValidateWindow(BinaryInputDeglitch, nameof(BinaryInputDeglitch));
        ValidateWindow(BinaryInputDebounceHoldoff, nameof(BinaryInputDebounceHoldoff));
    }

    private static void ValidateWindow(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero || value > TimeSpan.FromMilliseconds(25))
            throw new ArgumentOutOfRangeException(name, "Binary-input timing windows must be between 0 and 25 ms.");
    }
}

public enum MetrologyEventKind
{
    OutputApplied,
    RelayAdcSample,
    RelayMeasurementValid,
    RelayPickupAssert,
    RelayTripRequest,
    PickupContactFirstMake,
    TripContactFirstMake,
    TestSetBi2Pickup,
    TestSetBi1Trip,
    OutputStopCommand
}

public sealed record MetrologyEvent(
    MetrologyEventKind Kind,
    long Microseconds,
    string Detail)
{
    public double Milliseconds => Microseconds / 1_000d;
}

internal sealed class MetrologyClock
{
    public long Microseconds { get; private set; }

    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta));
        if (delta.Ticks % 10 != 0)
            throw new ArgumentOutOfRangeException(nameof(delta), "Metrology clock advances must resolve to whole microseconds.");
        Microseconds = checked(Microseconds + delta.Ticks / 10);
    }

    public void Reset() => Microseconds = 0;
}

internal sealed class MetrologyBinaryContact
{
    private readonly long _operateDelayUs;
    private readonly long _releaseDelayUs;
    private readonly long _bounceDurationUs;
    private readonly long _bouncePeriodUs;
    private bool _stableState;
    private bool _target;
    private long? _transitionDueUs;

    public MetrologyBinaryContact(
        TimeSpan operateDelay,
        TimeSpan releaseDelay,
        TimeSpan bounceDuration,
        TimeSpan bouncePeriod)
    {
        _operateDelayUs = ToMicroseconds(operateDelay);
        _releaseDelayUs = ToMicroseconds(releaseDelay);
        _bounceDurationUs = ToMicroseconds(bounceDuration);
        _bouncePeriodUs = bouncePeriod <= TimeSpan.Zero ? 0 : ToMicroseconds(bouncePeriod);
    }

    public void Request(bool requested, long nowUs)
    {
        // Do not commit a completed transition here. The 10 kHz BI sampler may still
        // need to inspect sample instants between the previous protection quantum and
        // nowUs. ClosedLoopVirtualTestBench commits only after that historical interval
        // has been sampled, preventing future stable state from leaking backward in time.
        if (requested == _target)
            return;
        _target = requested;
        _transitionDueUs = checked(nowUs + (requested ? _operateDelayUs : _releaseDelayUs));
    }

    public bool StateAt(long timestampUs)
    {
        if (_transitionDueUs is not { } due)
            return _stableState;
        if (timestampUs < due)
            return _stableState;
        if (_bounceDurationUs <= 0 || timestampUs >= due + _bounceDurationUs)
            return _target;
        if (_bouncePeriodUs <= 0)
            return _target;

        var phase = (timestampUs - due) / _bouncePeriodUs;
        return phase % 2 == 0 ? _target : !_target;
    }

    public long? FirstMakeAtMicroseconds
        => _target && _transitionDueUs is { } due ? due : null;

    public void Commit(long nowUs)
    {
        if (_transitionDueUs is not { } due)
            return;
        var settledAt = checked(due + _bounceDurationUs);
        if (nowUs < settledAt)
            return;
        _stableState = _target;
        _transitionDueUs = null;
    }

    public void Reset()
    {
        _stableState = false;
        _target = false;
        _transitionDueUs = null;
    }

    private static long ToMicroseconds(TimeSpan value)
    {
        if (value.Ticks % 10 != 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Contact timing must resolve to whole microseconds.");
        return value.Ticks / 10;
    }
}

internal sealed record MetrologyBinaryInputUpdate(
    bool State,
    long? RisingEdgeMicroseconds,
    long? FallingEdgeMicroseconds);

internal sealed class MetrologyBinaryInput
{
    private readonly int _samplePeriodUs;
    private readonly long _deglitchUs;
    private readonly long _debounceHoldoffUs;
    private long? _nextSampleUs;
    private bool _state;
    private bool? _candidate;
    private long? _candidateStartedUs;
    private long _holdoffUntilUs;

    public MetrologyBinaryInput(MetrologyTimingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        _samplePeriodUs = profile.BinaryInputSamplePeriodMicroseconds;
        _deglitchUs = profile.BinaryInputDeglitch.Ticks / 10;
        _debounceHoldoffUs = profile.BinaryInputDebounceHoldoff.Ticks / 10;
    }

    public bool State => _state;

    public MetrologyBinaryInputUpdate Observe(Func<long, bool> rawAt, long throughUs)
    {
        ArgumentNullException.ThrowIfNull(rawAt);
        _nextSampleUs ??= AlignUp(throughUs, _samplePeriodUs);
        long? rising = null;
        long? falling = null;

        while (_nextSampleUs <= throughUs)
        {
            var sampleAt = _nextSampleUs.Value;
            _nextSampleUs = checked(sampleAt + _samplePeriodUs);
            if (sampleAt < _holdoffUntilUs)
                continue;

            var raw = rawAt(sampleAt);
            if (raw == _state)
            {
                _candidate = null;
                _candidateStartedUs = null;
                continue;
            }

            if (_candidate != raw)
            {
                _candidate = raw;
                _candidateStartedUs = sampleAt;
            }

            var stableFor = sampleAt - _candidateStartedUs!.Value;
            if (_deglitchUs > 0 && stableFor < _deglitchUs)
                continue;

            _state = raw;
            _candidate = null;
            _candidateStartedUs = null;
            _holdoffUntilUs = checked(sampleAt + _debounceHoldoffUs);
            if (_state)
                rising ??= sampleAt;
            else
                falling ??= sampleAt;
        }

        return new MetrologyBinaryInputUpdate(_state, rising, falling);
    }

    public void Synchronize(long nowUs)
        => _nextSampleUs = AlignUp(nowUs, _samplePeriodUs);

    public void Reset()
    {
        _nextSampleUs = null;
        _state = false;
        _candidate = null;
        _candidateStartedUs = null;
        _holdoffUntilUs = 0;
    }

    private static long AlignUp(long value, int quantum)
    {
        var remainder = value % quantum;
        return remainder == 0 ? value : checked(value + quantum - remainder);
    }
}
