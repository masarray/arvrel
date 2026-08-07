#if ARIEC61850_SIBLING
using AR.Iec61850.Simulation;

namespace Arvrel.App.Controls.Avr;

/// <summary>
/// Compatibility facade around the sibling ARIEC61850 MMS server options.
///
/// ARVREL is often developed next to a locally checked-out ARIEC61850 repository.
/// That sibling may temporarily be on an older main revision which predates the
/// application process-control runtime extension. Referencing the new property
/// directly would make the whole WPF application fail to compile before the user
/// even has a chance to switch/update the sibling engine.
///
/// Keeping this facade in the ARVREL namespace makes the existing unqualified
/// IedSimulatorMmsServerOptions usage bind here. The implicit conversion creates
/// the real engine options and installs AssociationRuntimeFactory only when the
/// loaded engine actually exposes that capability.
/// </summary>
internal sealed class IedSimulatorMmsServerOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 102;
    public string ServerName { get; init; } = "ARIEC61850 Virtual IED";
    public Func<string, IMmsAssociationRuntime>? AssociationRuntimeFactory { get; init; }

    public static implicit operator AR.Iec61850.Simulation.IedSimulatorMmsServerOptions(
        IedSimulatorMmsServerOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var options = new AR.Iec61850.Simulation.IedSimulatorMmsServerOptions
        {
            Host = value.Host,
            Port = value.Port,
            ServerName = value.ServerName
        };

        if (value.AssociationRuntimeFactory is null)
            return options;

        var property = typeof(AR.Iec61850.Simulation.IedSimulatorMmsServerOptions)
            .GetProperty("AssociationRuntimeFactory");

        if (property is null || !property.CanWrite)
        {
            throw new InvalidOperationException(
                "The local ARIEC61850 sibling is older than the AVR process-control runtime required by ARVREL. " +
                "The ARVREL desktop application can run, but the IEC 61850 AVR server cannot enable SAS controls " +
                "until ARIEC61850 PR #52 (feature/mms-process-control-runtime) is checked out or merged.");
        }

        if (!property.PropertyType.IsInstanceOfType(value.AssociationRuntimeFactory))
        {
            throw new InvalidOperationException(
                $"The local ARIEC61850 AssociationRuntimeFactory contract is incompatible with this ARVREL build. " +
                $"Expected {property.PropertyType.FullName}; ARVREL provides {value.AssociationRuntimeFactory.GetType().FullName}. " +
                "Update both repositories to their matching process-control revisions.");
        }

        property.SetValue(options, value.AssociationRuntimeFactory);
        return options;
    }
}
#endif
