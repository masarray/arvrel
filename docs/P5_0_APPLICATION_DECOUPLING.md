# P5.0 — Application/Core Decoupling

## Purpose

P5.0 introduces a platform-neutral application boundary before the Avalonia migration. The current WPF application remains the shipping presentation layer, while deterministic laboratory state and workspace source authority can now be reused by another UI framework.

This phase is intentionally behavior-preserving. It does not change protection settings, operate timing, trust policy, evidence semantics, or the visual design.

## New dependency direction

```text
Arvrel.App (WPF presentation)
        |
        v
Arvrel.Application (workspace and laboratory orchestration)
        |
        v
Arvrel.Protection (deterministic protection and injection primitives)
```

`Arvrel.Application` targets `net8.0` and has no dependency on WPF, Windows desktop assemblies, ProcessBus capture, or platform-specific dialogs.

## Extracted responsibilities

### Deterministic laboratory source

`Arvrel.Application.Laboratory.DeterministicLabScenario` owns:

- the 4V + 4I virtual-injection runtime;
- deterministic sample progression;
- trust-degraded source state;
- active and effective output profiles;
- waveform sample arrays and measurement frames;
- source fingerprints and provenance.

It returns framework-neutral arrays through `ScenarioWaveform`. The existing WPF adapter converts those arrays into `WaveformFrame` without moving presentation types into the application layer.

### Internal laboratory session

`InternalLabSession` owns:

- START/STOP authority for the deterministic source;
- the internal `ProtectionEngine` lifecycle;
- scheduled protection evaluation;
- the latest `ProtectionSnapshot`;
- settings application;
- full laboratory reset;
- protection-only reset while retaining the configured and running injection.

### Workspace source authority

`ArvrelWorkspace` establishes a UI-neutral source-mode contract for:

- internal deterministic laboratory;
- live process bus;
- capture replay.

Selecting an external source stops the internal source. Starting an external source while the internal source is selected is rejected.

## WPF compatibility seam

`Arvrel.App.Services.DeterministicLabScenario` remains as a deliberately thin WPF adapter. It preserves the existing Main Window API and waveform behavior while delegating source generation to the platform-neutral scenario.

This seam allows the current application to remain stable while Avalonia views and controls are developed against the same application contracts.

## Explicitly deferred

The following work is outside P5.0 and should remain in dedicated follow-up pull requests:

- P5.1 packet-capture abstraction and Npcap/libpcap naming;
- Avalonia application shell;
- migration of file dialogs and dispatcher ownership;
- process-bus controller extraction from `MainWindow`;
- platform packaging and runtime capability reporting;
- visual redesign or control restyling.

## Acceptance checks

P5.0 is acceptable when:

1. `Arvrel.Application` and its tests build on plain `net8.0`.
2. No WPF namespace appears in `src/Arvrel.Application`.
3. The existing WPF application builds without a visual or settings change.
4. Deterministic waveform dimensions and measurement trust remain unchanged.
5. Internal source and external source authority cannot be active simultaneously.
6. Protection-only reset retains the active injection profile and source run state.
