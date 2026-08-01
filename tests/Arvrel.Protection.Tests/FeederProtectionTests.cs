using System.Numerics;
using Arvrel.Protection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Protection.Tests;

[TestClass]
public sealed class FeederProtectionTests
{
    [TestMethod]
    public void FundamentalEstimator_ReturnsRmsMagnitudeAndBalancedPositiveSequence()
    {
        const int samplesPerCycle = 80;
        var ia = BuildSine(samplesPerCycle, 2, 0);
        var ib = BuildSine(samplesPerCycle, 2, -120);
        var ic = BuildSine(samplesPerCycle, 2, 120);
        var zero = new double[samplesPerCycle];
        var va = BuildSine(samplesPerCycle, 63.5, 0);
        var vb = BuildSine(samplesPerCycle, 63.5, -120);
        var vc = BuildSine(samplesPerCycle, 63.5, 120);

        var phasors = FundamentalPhasorEstimator.Build(
            ia, ib, ic, zero,
            va, vb, vc, zero,
            samplesPerCycle,
            50);

        Assert.AreEqual(2, phasors.PhaseACurrent.Magnitude, 0.01);
        Assert.AreEqual(63.5, phasors.PhaseAVoltage.Magnitude, 0.05);
        Assert.AreEqual(2, phasors.PositiveSequenceCurrent.Magnitude, 0.02);
        Assert.IsTrue(phasors.NegativeSequenceCurrent.Magnitude < 0.02);
        Assert.AreEqual(63.5, phasors.PositiveSequenceVoltage.Magnitude, 0.05);
    }

    [TestMethod]
    public void Undervoltage27_OperatesAfterConfiguredDelay()
    {
        var settings = Settings(new FeederProtectionSettings
        {
            Undervoltage27Enabled = true,
            Undervoltage27PickupV = 90,
            Undervoltage27Delay = TimeSpan.FromMilliseconds(100),
            Undervoltage27Logic = VoltageSelectionLogic.TwoOfThree
        });
        var engine = new ProtectionEngine(settings);
        var phasors = BalancedPhasors(0.5, 70, 0);

        var snapshot = Run(engine, phasors, 150);

        Assert.IsTrue(snapshot.Feeder.Undervoltage27.Operated);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.AreEqual("27", snapshot.ActiveElement);
    }

    [TestMethod]
    public void Overvoltage59_OperatesAfterConfiguredDelay()
    {
        var settings = Settings(new FeederProtectionSettings
        {
            Overvoltage59Enabled = true,
            Overvoltage59PickupV = 110,
            Overvoltage59Delay = TimeSpan.FromMilliseconds(80),
            Overvoltage59Logic = VoltageSelectionLogic.OneOfThree
        });
        var engine = new ProtectionEngine(settings);
        var phasors = BalancedPhasors(0.5, 125, 0);

        var snapshot = Run(engine, phasors, 120);

        Assert.IsTrue(snapshot.Feeder.Overvoltage59.Operated);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.AreEqual("59", snapshot.ActiveElement);
    }

    [TestMethod]
    public void DirectionalPhase67_TripsForwardAndRestrainsReverse()
    {
        var settings = Settings(new FeederProtectionSettings
        {
            DirectionalPhase67Enabled = true,
            DirectionalPhase67PickupA = 1.25,
            DirectionalPhase67Delay = TimeSpan.FromMilliseconds(60),
            DirectionalPhase67CharacteristicAngleDeg = 45,
            DirectionalPhase67Sense = DirectionalSense.Forward,
            DirectionalPhase67MinimumPolarizingVoltageV = 5
        });

        var forwardEngine = new ProtectionEngine(settings);
        var forward = Run(forwardEngine, BalancedPhasors(2, 63.5, 45), 100);
        Assert.IsTrue(forward.Feeder.DirectionalPhase67.Operated, forward.Feeder.PhaseDirectionalDecision.Reason);
        Assert.IsTrue(forward.TripLatched);
        Assert.IsTrue(forward.Feeder.PhaseDirectionalDecision.InSelectedDirection);

        var reverseEngine = new ProtectionEngine(settings);
        var reverse = Run(reverseEngine, BalancedPhasors(2, 63.5, -135), 150);
        Assert.IsFalse(reverse.Feeder.DirectionalPhase67.Pickup, reverse.Feeder.PhaseDirectionalDecision.Reason);
        Assert.IsFalse(reverse.TripLatched);
        Assert.IsFalse(reverse.Feeder.PhaseDirectionalDecision.InSelectedDirection);
    }

    [TestMethod]
    public void DirectionalEarth67N_UsesResidualVoltageAndCurrent()
    {
        var settings = Settings(new FeederProtectionSettings
        {
            DirectionalEarth67NEnabled = true,
            DirectionalEarth67NPickupA = 0.30,
            DirectionalEarth67NDelay = TimeSpan.FromMilliseconds(80),
            DirectionalEarth67NCharacteristicAngleDeg = -45,
            DirectionalEarth67NSense = DirectionalSense.Forward,
            DirectionalEarth67NMinimumPolarizingVoltageV = 2
        });
        var engine = new ProtectionEngine(settings);
        var current = Polar(0.2, -45);
        var voltage = Polar(5, 0);
        var phasors = new PhasorMeasurementSet(
            current, current, current, current * 3,
            voltage, voltage, voltage, voltage * 3,
            50);

        var snapshot = Run(engine, phasors, 120);

        Assert.IsTrue(snapshot.Feeder.DirectionalEarth67N.Operated, snapshot.Feeder.EarthDirectionalDecision.Reason);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.AreEqual("67N", snapshot.ActiveElement);
        Assert.IsTrue(snapshot.EarthPickup);
    }

    [TestMethod]
    public void ResidualOvervoltage59N_OperatesFromThreeVZero()
    {
        var settings = Settings(new FeederProtectionSettings
        {
            ResidualOvervoltage59NEnabled = true,
            ResidualOvervoltage59NPickupV = 10,
            ResidualOvervoltage59NDelay = TimeSpan.FromMilliseconds(50)
        });
        var engine = new ProtectionEngine(settings);
        var voltage = Polar(4, 0);
        var phasors = new PhasorMeasurementSet(
            Complex.Zero, Complex.Zero, Complex.Zero, Complex.Zero,
            voltage, voltage, voltage, voltage * 3,
            50);

        var snapshot = Run(engine, phasors, 80);

        Assert.IsTrue(snapshot.Feeder.ResidualOvervoltage59N.Operated);
        Assert.IsTrue(snapshot.TripLatched);
        Assert.AreEqual(12, snapshot.Feeder.ResidualOvervoltage59N.OperatingQuantity, 0.001);
    }

    [TestMethod]
    public void EnabledFeederFunctions_RemainSecureWithoutPhasorEvidence()
    {
        var settings = Settings(new FeederProtectionSettings
        {
            DirectionalPhase67Enabled = true,
            Undervoltage27Enabled = true
        });
        var engine = new ProtectionEngine(settings);
        var timestamp = DateTimeOffset.UtcNow;

        var snapshot = engine.Evaluate(new MeasurementFrame(timestamp, 0.5, 0.5, 0.5, 0, SmvTrustState.Healthy));

        Assert.IsFalse(snapshot.TripLatched);
        Assert.AreEqual("PHASOR UNAVAILABLE", snapshot.Feeder.ActiveElement);
    }

    private static ProtectionSettings Settings(FeederProtectionSettings feeder) => new()
    {
        PhaseInstantaneousEnabled = false,
        PhaseTimeEnabled = false,
        EarthInstantaneousEnabled = false,
        EarthTimeEnabled = false,
        Feeder = feeder
    };

    private static ProtectionSnapshot Run(ProtectionEngine engine, PhasorMeasurementSet phasors, int durationMs)
    {
        var timestamp = DateTimeOffset.UtcNow;
        ProtectionSnapshot snapshot = ProtectionSnapshot.Ready(timestamp);
        for (var elapsed = 0; elapsed <= durationMs; elapsed += 5)
        {
            snapshot = engine.Evaluate(new MeasurementFrame(
                timestamp.AddMilliseconds(elapsed),
                phasors.PhaseACurrent.Magnitude,
                phasors.PhaseBCurrent.Magnitude,
                phasors.PhaseCCurrent.Magnitude,
                phasors.ResidualCurrent.Magnitude,
                SmvTrustState.Healthy,
                phasors));
        }
        return snapshot;
    }

    private static PhasorMeasurementSet BalancedPhasors(double current, double voltage, double currentAngle)
    {
        return new PhasorMeasurementSet(
            Polar(current, currentAngle),
            Polar(current, currentAngle - 120),
            Polar(current, currentAngle + 120),
            Complex.Zero,
            Polar(voltage, 0),
            Polar(voltage, -120),
            Polar(voltage, 120),
            Complex.Zero,
            50);
    }

    private static double[] BuildSine(int count, double rms, double angleDegrees)
    {
        var values = new double[count];
        var phase = angleDegrees * Math.PI / 180;
        for (var index = 0; index < count; index++)
            values[index] = rms * Math.Sqrt(2) * Math.Sin(2 * Math.PI * index / count + phase);
        return values;
    }

    private static Complex Polar(double magnitude, double angleDegrees)
        => Complex.FromPolarCoordinates(magnitude, angleDegrees * Math.PI / 180);
}
