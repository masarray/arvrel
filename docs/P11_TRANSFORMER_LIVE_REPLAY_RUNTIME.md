# P11 — Live / Replay Transformer Protection Runtime

## Objective

P11 connects the transformer protection research layers introduced in P8, P9 and P10 into one stateful runtime that can consume the existing ARVREL process-bus controller during either live capture or PCAP/PCAPNG replay.

The runtime remains virtual-only. It does not publish GOOSE, MMS commands, binary outputs, or any physical trip signal.

## Layered architecture

```text
SmvProcessBusController
    |
    | StreamUpdated (accepted or rejected ingress transaction)
    v
TransformerProcessBusProtectionRuntime
    |
    | read selected raw-secondary HV/LV stream snapshots
    v
TransformerProtectionRuntime
    |
    +--> P9  TransformerProcessBusAdapter
    |       - distinct stream identity
    |       - smpSynch policy
    |       - smpCnt alignment
    |       - capture-skew guard
    |       - one-sample phase correction
    |
    +--> P10 TransformerHarmonicEstimator
    |       - coherent H1/H2/H5 DFT
    |       - measured H2/H1 and H5/H1
    |
    +--> TransformerEngineeringAdapter
    |       - transformer MVA / kV
    |       - CT ratios and polarity
    |       - vector-group compensation
    |       - common per-unit current base
    |
    +--> P8 TransformerProtectionEngine
            - 87T standard two-slope biased differential
            - 87T-HS
            - REF HV / REF LV
            - harmonic blocking / restraint
            - independent SMV trust permissions
            - virtual trip latch
```

## Runtime configuration

`TransformerProtectionRuntimeConfiguration` binds one runtime instance to:

- one exact HV SV stream key;
- one exact LV SV stream key;
- transformer nameplate data;
- HV and LV phase / neutral CT engineering;
- transformer protection settings;
- P9 pairing policy;
- P10 harmonic-estimator policy.

The two stream keys must be different. The configuration is validated before the runtime starts, and `TransformerEngineeringAdapter.Build(...)` must succeed before any protection evaluation is allowed.

## Effective protection settings

Operator protection settings are not evaluated directly against raw CT-secondary amperes.

At runtime construction:

```text
operator settings
      +
transformer / CT engineering
      |
      v
TransformerEngineeringPlan.ApplyTo(...)
      |
      v
effective TransformerProtectionSettings
      |
      +--> HV/LV current-to-pu scale
      +--> vector-group phase compensation
      +--> polarity correction
      +--> differential zero-sequence rule
      +--> REF neutral-CT scaling
```

The effective settings fingerprint is carried in every runtime snapshot and evidence object.

## Live and replay event model

P11 adds a public `SmvProcessBusController.StreamUpdated` event.

The controller raises one event after each SV ingress transaction that reaches a stream runtime:

- accepted payload;
- rejected duplicate;
- rejected out-of-order frame.

The event contains:

- stream key;
- capture timestamp;
- whether the source is replay;
- whether the payload was accepted.

`TransformerProcessBusProtectionRuntime` subscribes only to the selected HV/LV stream keys. Therefore unrelated SV traffic cannot drive the transformer runtime.

This event-driven model is important for replay. `ReplayAsync(...)` may process a capture much faster than wall-clock time; evaluating only after replay completion would inspect only the final snapshot and would not reproduce protection timing through the capture. P11 evaluates as the frames pass through the controller.

## Pair identity and duplicate evaluation guard

A protection pair is identified by:

```text
HV smpCnt
LV smpCnt
HV capture timestamp
LV capture timestamp
```

If the exact same pair is read again, P11 returns the current runtime snapshot with:

```text
EvaluatedNewPair = false
```

and does not call `TransformerProtectionEngine.Evaluate(...)` again.

This prevents UI refreshes or repeated snapshot reads from artificially advancing a definite-time protection timer.

## Invalid-pair timer security

A protection timer must never bridge an interval in which a valid paired transformer measurement did not exist.

Example:

```text
valid internal-fault pair     -> 87T timing
valid internal-fault pair     -> 10 ms accumulated
HV/LV pairing becomes invalid -> no trustworthy transformer measurement
... gap ...
valid pair returns            -> timer must restart, not inherit the gap
```

Before a virtual trip has latched, P11 handles an invalid pair by rebuilding the deterministic `TransformerProtectionEngine` from the same effective settings. The first recovered valid pair therefore starts with `delta = 0`.

This avoids treating missing, misaligned or invalid process-bus data as fault persistence.

## Trip-latch freeze

Once `TripLatched=true`, P11 freezes the protection decision until explicit runtime reset.

Later valid pairs may update the aligned measurement evidence, but they do not rewrite the latched operated element. Later invalid pairs also do not clear the historical latch.

This produces deterministic relay-style evidence:

```text
first valid operate decision
        |
        v
TRIP LATCHED
        |
        +--> measurement may continue updating
        +--> pair diagnostics may change
        +--> operated protection snapshot remains frozen
        |
        v
explicit Reset()
```

`UpdateConfiguration(..., keepTripLatch: true)` can intentionally preserve a latched trip across an engineering/settings refresh. The default is to clear it.

## Trust behavior

P11 does not invent a new trust policy. Trust remains layered:

1. stream runtime determines IEC 61850 / continuity / quality / scaling / SCL trust;
2. P9 determines whether two streams form an acceptable transformer pair;
3. P10 determines whether the harmonic window is trustworthy;
4. P8 decides whether measurement, pickup and trip permissions are allowed.

A `SmvTrustState.TripBlocked(...)` input can therefore still produce 87T operate evidence while suppressing `TripRequested`.

This distinction is preserved in the runtime snapshot as `ProtectionBlocked`.

## Runtime states

P11 publishes a compact state machine:

- `WaitingForPair` — selected streams or complete windows are not ready;
- `PairBlocked` — P9/P10 pairing or harmonic validation rejected the current input;
- `ProtectionBlocked` — a valid measurement exists but trust prevents a trip request;
- `Ready` — valid paired measurement, no active protection pickup;
- `Pickup` — protection is starting/timing;
- `TripLatched` — virtual trip decision is latched and frozen until reset.

## Source modes

The event-driven bridge derives source mode directly from the process-bus controller:

```text
controller.IsReplayMode -> PcapReplay
controller.IsRunning    -> LiveCapture
otherwise               -> InternalDemo/manual evaluation
```

The pure `TransformerProtectionRuntime` core can also be exercised directly with supplied `SmvRuntimeSnapshot` objects. This keeps deterministic unit tests separate from packet-capture transport tests.

## Evidence snapshot

`CaptureEvidence()` returns one immutable `TransformerProtectionRuntimeEvidence` record containing:

- export timestamp;
- source mode;
- selected HV/LV stream keys;
- transformer nameplate;
- parsed vector group;
- HV/LV CT engineering;
- effective protection settings;
- effective settings fingerprint;
- pairing diagnostics;
- exact pair identity;
- enriched transformer measurement frame;
- protection snapshot;
- runtime decision reason.

P11 provides the evidence model only. A file exporter / report format can be added as a later presentation layer without changing protection behavior.

## Deterministic validation

Core tests cover:

- compensated through-current stability;
- sustained internal-fault 87T trip;
- same-pair deduplication;
- invalid-pair timer reset;
- waveform H2 driving inrush block;
- `TripBlocked` trust preserving operate evidence while suppressing virtual trip;
- trip-latch freeze through later valid and invalid inputs;
- explicit reset;
- engineering-plan application and evidence capture;
- invalid same-stream configuration rejection.

Integration tests exercise the actual `SmvProcessBusController` with synthetic IEC 61850 SV Ethernet frames:

1. replay once to discover deterministic stream keys;
2. attach the P11 transformer runtime;
3. replay the same dual-stream capture and verify frame-by-frame runtime updates;
4. run the same frame sequence through an injected live capture backend and verify `LiveCapture` runtime operation while the capture task remains active.

The integration capture intentionally omits SCL binding, so existing trust policy produces operate evidence with trip blocked. This verifies that P11 does not bypass the established SCL/trust boundary.

## Intentional non-goals

P11 does not add:

- practitioner UI;
- JSON/PDF evidence exporter;
- GOOSE/MMS/physical trip output;
- CT-saturation classification;
- adaptive frequency tracking;
- fourth-harmonic logic;
- cross-phase harmonic blocking;
- IEC 60255 conformance/type-test claims;
- vendor-equivalence claims.

## Next recommended layer

After P11, the protection path is executable end-to-end from live/replay Sampled Values.

The next useful layer is a practitioner-facing transformer IED UI that exposes:

- HV/LV stream selection;
- transformer / CT / vector-group engineering;
- Is1 / K1 / Is2 / K2 characteristic;
- per-phase Idiff / Ibias / threshold;
- measured H2 / H5;
- REF HV/LV quantities;
- trust / pairing status;
- virtual trip latch and reset;
- evidence capture/export action.

CT-saturation secure/operate scenarios should remain a separate protection-research PR so UI work does not silently change the algorithm.
