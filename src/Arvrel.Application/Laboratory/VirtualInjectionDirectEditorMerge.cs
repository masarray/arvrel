using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

/// <summary>
/// Applies the fields exposed by the simple direct editor while retaining advanced
/// source-event and CT parameters that are intentionally read-only on that surface.
/// </summary>
public static class VirtualInjectionDirectEditorMerge
{
    public static VirtualInjectionProfile Apply(
        VirtualInjectionProfile activeProfile,
        string name,
        double frequencyHz,
        IReadOnlyDictionary<VirtualInjectionSignal, VirtualInjectionChannel> editedChannels)
    {
        ArgumentNullException.ThrowIfNull(activeProfile);
        ArgumentNullException.ThrowIfNull(editedChannels);
        activeProfile = activeProfile.Normalize();

        VirtualInjectionChannel Merge(VirtualInjectionSignal signal)
        {
            if (!editedChannels.TryGetValue(signal, out var edited))
                throw new ArgumentException($"Edited channel '{signal}' is missing.", nameof(editedChannels));
            var previous = activeProfile.Channel(signal);
            return edited with
            {
                DcOffsetPercent = previous.DcOffsetPercent,
                DcTimeConstantMilliseconds = previous.DcTimeConstantMilliseconds
            };
        }

        return new VirtualInjectionProfile(
            name,
            frequencyHz,
            Merge(VirtualInjectionSignal.PhaseAVoltage),
            Merge(VirtualInjectionSignal.PhaseBVoltage),
            Merge(VirtualInjectionSignal.PhaseCVoltage),
            Merge(VirtualInjectionSignal.NeutralVoltage),
            Merge(VirtualInjectionSignal.PhaseACurrent),
            Merge(VirtualInjectionSignal.PhaseBCurrent),
            Merge(VirtualInjectionSignal.PhaseCCurrent),
            Merge(VirtualInjectionSignal.NeutralCurrent))
        {
            CurrentTransformer = activeProfile.CurrentTransformer
        }.Normalize();
    }
}
