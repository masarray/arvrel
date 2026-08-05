# P5.0 — Application/core decoupling

## Objective

P5.0 establishes a platform-neutral application boundary before the Avalonia migration. The current WPF application remains operational, but deterministic laboratory behavior is no longer defined by WPF controls or dispatcher code.

## Dependency rule

```text
Arvrel.App (WPF presentation)
        ↓
Arvrel.Application (workspace and laboratory orchestration)
        ↓
Arvrel.Protection (deterministic algorithms and models)
```

`Arvrel.Application` targets plain `net8.0` and must not reference WPF, Avalonia, Windows desktop APIs, dialogs, brushes, controls, or dispatchers.

## Delivered boundary

### Platform-neutral deterministic source

`Arvrel.Application.Laboratory.DeterministicLabScenario` owns the virtual-injection runtime and exposes:

- deterministic measurement frames;
- current waveform arrays;
- frequency, sample rate, and samples-per-cycle metadata;
- source start/stop, profile, fault, and SMV-degradation state;
- source fingerprints and provenance.

Waveform output is represented by `ScenarioWaveform`, not by a presentation-framework control type.

### Internal laboratory session

`InternalLabSession` owns the relationship between:

- the deterministic source;
- `ProtectionEngine`;
- the current `ProtectionSnapshot`;
- source START/STOP authority;
- scheduled and single-step evaluation;
- settings and reset lifecycle.

The session provides two reset contracts:

- full reset clears protection state, source state, and optionally the configured profile;
- protection-only reset clears timers and the trip latch while retaining the configured profile and source run state.

This gives future presentation layers one application service instead of independently recreating engine and scenario ownership.

### Workspace lifecycle

`ArvrelWorkspace` introduces a small source-mode state model for:

- internal laboratory;
- live process bus;
- capture replay.

It prevents the internal laboratory and an external source from being marked active at the same time. Live-capture implementation remains in the existing process-bus layer for P5.1.

### WPF compatibility adapter

The existing `Arvrel.App.Services.DeterministicLabScenario` remains available with its previous public API. It delegates simulation to `Arvrel.Application` and only converts `ScenarioWaveform` into the existing WPF `WaveformFrame`.

This keeps the current `MainWindow` and its partial classes behavior-compatible while removing the simulation-to-WPF dependency.

## Validation

`Arvrel.Application.Tests` verifies that:

- deterministic waveform output is framework-neutral and dimensionally correct;
- protection evaluation advances only while the application session is running;
- source START/STOP and the protection session share one run-state authority;
- protection-only reset retains the active profile and running source;
- switching to an external source stops the internal laboratory;
- invalid simultaneous source state is rejected.

The existing solution build continues to compile the WPF shell and all protection/process-bus regression tests.

## Acceptance checks

P5.0 is acceptable when:

1. `Arvrel.Application` and its tests build on plain `net8.0`.
2. No WPF namespace appears in `src/Arvrel.Application`.
3. The existing WPF application builds without a visual or settings change.
4. Deterministic waveform dimensions and measurement trust remain unchanged.
5. Internal and external source authority cannot be active simultaneously.
6. Protection-only reset retains the active injection profile and source run state.

## Deliberately out of scope

P5.0 does not:

- add Avalonia packages or an Avalonia executable;
- replace `MainWindow` with MVVM in one step;
- rename or abstract the Npcap/libpcap transport;
- alter protection algorithms, settings, timing, trip policy, or evidence schema;
- change the current WPF appearance or user workflow.

## Next boundary

P5.1 should move process-bus source lifecycle behind platform-neutral capture and replay interfaces. After that, an Avalonia shell can consume `Arvrel.Application` without importing WPF or Windows-specific packet-capture assumptions.
