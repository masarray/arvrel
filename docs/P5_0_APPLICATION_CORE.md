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
- run/pause state;
- settings and reset lifecycle.

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
- switching to an external source stops the internal laboratory;
- invalid simultaneous source state is rejected.

The existing solution build continues to compile the WPF shell and all protection/process-bus regression tests.

## Deliberately out of scope

P5.0 does not:

- add Avalonia packages or an Avalonia executable;
- replace `MainWindow` with MVVM in one step;
- rename or abstract the Npcap/libpcap transport;
- alter protection algorithms, settings, timing, trip policy, or evidence schema;
- change the current WPF appearance or user workflow.

## Next boundary

P5.1 should move process-bus source lifecycle behind platform-neutral capture and replay interfaces. After that, an Avalonia shell can consume `Arvrel.Application` without importing WPF or Windows-specific packet-capture assumptions.
