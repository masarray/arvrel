# ARVREL architecture

> Current shipped architecture for **v0.1.0-beta.6**. For the concise release-status authority, see [`CURRENT_STATUS.md`](CURRENT_STATUS.md). Historical `P*` documents remain point-in-time design records.

## Repository boundary

`masarray/arvrel` owns the stable Windows WPF product: the P6 feeder virtual-relay interface, closed-loop laboratory orchestration, protection algorithms, transformer/AVR workspaces, process-bus orchestration, trust policy, evidence presentation, and official Windows packaging.

The sibling `masarray/ARIEC61850` repository remains the source of truth for reusable IEC 61850 frame, SCL, Sampled Values, and native transport primitives.

```text
Git/
├── ARIEC61850/
└── arvrel/
```

Cross-platform Avalonia development is maintained separately in `masarray/arvrel-avalonia`.

## Dependency direction

```text
Arvrel.App (Windows WPF / P6 presentation)
        │
        ├── Arvrel.Application
        ├── Arvrel.ProcessBus ── Arvrel.Capture
        └── Arvrel.Protection
```

- `Arvrel.App` owns WPF XAML, operator interaction, display formatting, desktop lifecycle, and rendering.
- `Arvrel.Application` owns deterministic laboratory orchestration, closed-loop virtual wiring, metrology timing, causal relay-front-end behavior, and immutable workspace state used by the WPF product.
- `Arvrel.Capture` owns packet-source contracts and PCAP/PCAPNG replay.
- `Arvrel.ProcessBus` owns stream discovery, SCL binding, decode orchestration, continuity, measurement windows, trust, transformer paired-SV runtime, and evidence projection.
- `Arvrel.Protection` owns deterministic feeder/transformer protection algorithms, timers, operation records, and relay trip-latch semantics.

Shared engineering behavior must never derive protection timing or TESTSET timing from WPF dispatcher cadence.

## Closed-loop feeder authority

Beta.6 separates the virtual test set and virtual relay as equipment authorities even though both run inside one application process:

```text
TESTSET metrology clock T0
        ↓
configured secondary source
        ↓
instantaneous waveform
        ↓
virtual analog wiring
        ↓
relay terminal samples
        ↓
signed clipping / ADC quantization / configured input delay
        ↓
causal rolling relay measurement
        ↓
ProtectionEngine
        ↓
relay pickup / timer / TripLatched request
        ↓
BO operate delay / contact behavior
        ↓
virtual binary wiring
        ↓
independent TESTSET BI sampling / deglitch / debounce
        ↓
accepted BI edge
        ↓
TESTSET timing result / optional source auto-stop
```

### External-I/O timing invariant

`ProtectionSnapshot.TripLatched` is an internal relay state. It may request BO1 but is **not** the TESTSET trip result. The external measured trip and optional auto-stop authority come only from the accepted wired `TESTSET.BI1` rising edge.

This invariant is regression-tested with an open BO1→BI1 trip wire: relay internal trip remains possible, while the TESTSET records no trip and does not auto-stop.

### Clock domains

The default desktop closed-loop profile has independent modeled clocks:

| Domain | beta.6 behavior |
|---|---|
| TESTSET metrology clock | monotonic integer µs, 1 µs resolution |
| TESTSET BI sampler | 10 kHz / 100 µs |
| BI deglitch | 0.5 ms |
| BI debounce holdoff | 0 ms |
| Relay source/acquisition processing | 4 kHz / 250 µs |
| WPF refresh | presentation cadence only |

A measured T0→BI duration need not be an exact multiple of 100 µs because T0 and the free-running BI sample grid may have phase offset. The accepted input edge nevertheless belongs to the BI sampling domain, not to WPF time.

### Causal relay front end

The closed-loop feeder path no longer treats source-side RMS/phasor values as relay ADC input. The relay consumes instantaneous signed terminal samples and applies:

1. virtual terminal wiring;
2. signed clipping against the configured peak input range;
3. signed ADC quantization;
4. configured input/filter delay;
5. a causal one-cycle rolling DFT using only samples that have arrived.

The default behavioral profile uses 16-bit equivalent conversion, 20 A RMS current full scale, 300 V RMS voltage full scale, 4 kHz acquisition, 1.5 ms input/filter delay, and one nominal 50 Hz measurement cycle.

A powered numerical relay is already sampling before a test starts. Each stopped-source run therefore begins with settled pre-fault history rather than an empty DFT window.

These parameters model generic numerical-relay behavior; they do not claim a manufacturer-specific analog front end or calibrated uncertainty.

## Timing semantics

Generic output BO2 is intentionally **ANY PICKUP**. Therefore these timestamps are distinct:

- first relay ANY PICKUP that requests BO2;
- accepted `TESTSET.BI2` edge;
- pickup of the element that ultimately operates;
- that operated element's pickup-to-trip interval;
- live relay trip request that requests BO1;
- accepted `TESTSET.BI1` trip edge.

The operated-element P→T must be correlated with `LatchedOperation.Element`, never with generic BI2 time. A 60 ms 50P definite delay exactly representable on the 250 µs relay grid produces an exact 60.000 ms element P→T in the reference engine.

## Reset, freeze, and re-arm authority

A TESTSET BI1 auto-stop turns source output off but intentionally retains the completed run as **OUTPUT OFF · FROZEN CAPTURE**.

Relay RESET runs one deterministic `ClosedLoopRelayResetTransaction`:

1. while output is OFF, advance causal acquisition in 250 µs quanta until stale fault pickup releases;
2. clear relay latch/timers once;
3. continue advancing modeled feedback until relay trip latch is clear, no protection pickup remains, BO1/BO2 are LOW, and TESTSET BI1/BI2 are LOW;
4. expose **READY TO RE-ARM** only after the postcondition is true.

The transaction has a bounded 100 ms simulated timeout and preserves completed timing/frozen evidence. Relay RESET does not change source setpoints or restart output. If source output is still energized, protection may legitimately reassert.

## Process-bus pipeline

```text
Live Npcap backend or PCAP/PCAPNG replay
        ↓
Timestamped Ethernet frame
        ↓
ARIEC61850 Sampled Values decoder
        ↓
Stream identity + optional SCL binding
        ↓
Mapping / scaling / quality / continuity
        ↓
Per-stream sample rings
        ↓
Complete measurement/evidence windows
        ↓
SMV trust policy
        ↓
Protection or transformer runtime
        ↓
Immutable presentation + evidence snapshot
```

Duplicate/out-of-order frames remain visible as diagnostics but are rejected before measurement/protection admission.

## Transformer Differential path

Internal synchronized two-sided injection and external paired-SV evaluation share the existing transformer protection runtime rather than implementing a UI-local duplicate algorithm.

```text
Internal synchronized HV/LV/NGR source
                     ┐
                     ├─> TransformerProtectionRuntime -> 87T / 87T-HS / REF evidence
Paired HV/LV SV path ┘
```

The external path additionally evaluates stream identity, synchronization, `smpCnt`, `smpSynch`, frequency, mapping, scaling, and trust. Independent neutral/NGR evidence is required for REF; calculated phase residual is not silently promoted to a neutral CT.

## AVR / OLTC + MMS path

```text
virtual transformer plant
        ↕
AVR / OLTC controller
        ↕
virtual process interlocks / authority
        ↕
IEC 61850 MMS model
(browse/read · DataSets · reports · GI/integrity · modeled SBO/SBOw controls)
```

MMS commands can change only the virtual AVR/OLTC process when modeled authority/interlocks permit. They provide no physical OLTC motor or primary-equipment authority.

## P6 WPF presentation boundary

P6 is the presentation authority for the public virtual-relay faceplate geometry, controls, LCD, annunciation, timing strip, and operator commands. It consumes shared runtime state; it does not instantiate a second protection engine and does not infer operation from lamp state.

The WPF presentation loop may observe many 250 µs relay quanta inside a slower visual refresh slice. This is deliberate: rendering cadence is not timing authority.

## Threading and state

- live capture runs outside the WPF dispatcher and yields timestamped frames through an asynchronous backend contract;
- PCAP replay streams frames from disk;
- mutable runtime state is isolated behind shared-layer ownership and immutable snapshots;
- protection evaluation occurs when modeled acquisition or decoded data advances, not when WPF paints;
- source, wiring, settings, and reset changes enter shared layers as complete validated transactions;
- completed test evidence can remain frozen while current output state has already returned to zero.

## Trust boundary

A stream may remain diagnostically visible while new protection/control authority is blocked. Trust evaluation includes complete windows, payload decode health, mapping/scaling provenance, freshness, `smpCnt` continuity, quality, SCL identity, `svID`, dataset, `confRev`, and source context.

For internal closed-loop injection, trust does not replace the external TESTSET BI path. Relay internal state and TESTSET measurement remain separate authorities.

## Safety boundary

- outputs are virtual only;
- no calibrated measurement/test-set, IEC 61850 conformance, IEC 60255 type-test, commissioning-acceptance, or deterministic hard-real-time claim is made;
- no operational GOOSE trip, physical relay contact, physical OLTC motor command, autonomous switching, or primary-equipment authority exists;
- IEC 61850 MMS control is implemented only against the virtual AVR/OLTC process;
- Windows, Npcap, adapter drivers, publisher behavior, and host load influence live capture behavior;
- future hardware/device-specific fidelity requires measured transfer functions, thresholds, burdens, timing uncertainty, and manufacturer-specific processing evidence before any stronger claim is appropriate.
