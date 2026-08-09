# ARVREL Product Requirements Document

> Current product definition for the Windows WPF line as of **v0.1.0-beta.6**. Historical milestone PRDs/design notes remain useful for design history but do not override [`CURRENT_STATUS.md`](CURRENT_STATUS.md), the selected release, or current tests.

## Product definition

ARVREL is a vendor-neutral Windows **virtual protection and control IED laboratory**. It is not merely a Sampled Values analyzer and not merely a relay-faceplate simulation.

The shipped product combines four engineering chains:

```text
Feeder closed-loop secondary injection
TESTSET source → virtual analog wire → causal relay front end → protection
     ↑                                                   ↓
     └── timing/auto-stop ← TESTSET BI ← virtual wire ← relay BO

IEC 61850 process bus
Live/Replay SV → identity/mapping/scaling/continuity/trust → measurement → protection → evidence

Transformer Differential
Internal synchronized HV/LV/NGR or paired external SV → engineering compensation → 87T/87T-HS/REF → evidence

AVR / OLTC
Virtual transformer plant ↔ AVR/OLTC logic ↔ virtual interlocks/authority ↔ laboratory MMS model
```

## Public product goals

ARVREL should let an engineer:

1. observe the accepted signal source and engineering context;
2. see which measured quantities enter protection/control logic;
3. distinguish relay internal behavior from external virtual-I/O behavior;
4. reproduce protection pickup, timing, trip, restraint, and reset causally;
5. exercise virtual transformer differential and AVR/OLTC workflows without physical output authority;
6. preserve enough evidence to explain why a virtual operation occurred or did not occur.

## Locked safety boundary

- outputs remain virtual only;
- no physical relay contact, physical OLTC motor command, operational GOOSE trip, autonomous switching, or primary-equipment authority;
- no claim of calibrated test-set performance, IEC 60255 type-test evidence, IEC 61850 conformance certification, commissioning acceptance, or protection-grade hard-real-time timing;
- laboratory MMS controls may change only the modeled AVR/OLTC process;
- live Ethernet work is restricted to authorized isolated laboratory networks.

## Feeder closed-loop P0 acceptance

### External TESTSET authority

Measured trip and optional auto-stop must follow only:

```text
relay trip request → BO1 behavior → virtual trip wire → TESTSET BI1 acceptance
```

Directly reading internal relay trip state as a TESTSET result is forbidden.

Required regression: with BO1→BI1 disconnected, the relay may trip internally but the TESTSET must record no external trip time and must not auto-stop from trip.

### Timing domains

The beta.6 reference profile requires:

- monotonic integer-microsecond TESTSET clock;
- 1 µs metrology clock resolution;
- 10 kHz TESTSET BI sampling;
- independent deglitch and debounce semantics;
- 4 kHz / 250 µs relay/source acquisition grid;
- WPF rendering excluded from timing authority.

### Causal relay acquisition

The closed-loop relay input must be based on instantaneous signed terminal samples and model clipping, quantization, configured input delay, and a causal rolling measurement. It must not receive source-side RMS/phasor values as a substitute for an ADC/front-end path.

The stopped-source start condition must represent a powered relay with settled pre-fault history rather than an empty measurement window.

### Timing semantics

The UI/evidence must not conflate:

- generic ANY PICKUP / BO2 request;
- accepted TESTSET BI2;
- pickup of the element that ultimately operates;
- operated-element P→T;
- live relay trip request / BO1 request;
- accepted TESTSET BI1.

A 60 ms definite-time 50P setting exactly representable on the 250 µs relay grid should produce an exact 60.000 ms operated-element P→T in the reference engine.

### Reset/re-arm

One relay RESET command after BI1 auto-stop must deterministically settle stale causal acquisition, clear relay latch/timers once, release BO1/BO2 and TESTSET BI1/BI2, preserve completed evidence, leave source output OFF, and report READY TO RE-ARM only after the full postcondition is met.

## Process-bus requirements

- live Npcap capture and PCAP/PCAPNG replay;
- SCL-assisted binding plus reviewed fallback where supported;
- APPID/MAC/VLAN/`svID`/dataset/`confRev` evidence;
- mapping/scaling provenance;
- freshness, quality, continuity, complete-window, and source-context trust;
- duplicate/out-of-order frames visible diagnostically but rejected before measurement/protection admission;
- distinct `AllowsMeasurement`, `AllowsPickup`, and `AllowsTrip` authority.

Uncertain process-bus data must produce an explicit reasoned block rather than a silent trip decision.

## Feeder protection scope

Public feeder protection includes 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N, with practitioner setting groups, revisions, fingerprints, timers, event/operation evidence, and virtual trip latch.

## Transformer Differential requirements

The public Transformer IED must retain:

- 87T with generic dual-slope restraint semantics;
- 87T-HS;
- REF HV and REF LV with independent neutral/NGR evidence;
- H2/H5 security;
- context-gated external-fault/CT-saturation security;
- CT ratio, polarity, transformer rating, and supported vector-group compensation;
- deterministic 10-scenario self-test;
- synchronized internal two-sided injection;
- paired-SV live/replay workflow with synchronization and trust checks.

Calculated phase residual must not silently become independent neutral-CT evidence for REF.

## AVR / OLTC requirements

The public AVR/OLTC workspace must retain:

- simulated transformer plant;
- 17-position OLTC;
- modeled LOCAL/REMOTE and AUTO/MANUAL authority;
- AVR regulation/deadband/interlocks;
- IEC 61850 MMS browse/read;
- DataSets, reports, GI/integrity;
- modeled SBO/SBOw controls and virtual settings;
- evidence showing accepted/rejected virtual control cause.

MMS commands terminate inside the virtual process.

## Evidence requirements

A reviewable operation should preserve enough context to identify:

- product version and run/source identity;
- settings identity/fingerprint;
- input/source provenance and virtual wiring when relevant;
- trust state and permission decisions;
- measured operating quantities;
- first generic pickup and source;
- operated element and its own pickup/trip timing;
- relay trip request;
- external TESTSET BI acceptance and timing resolution;
- output stop/frozen-capture relationship;
- reset/re-arm state;
- transformer or AVR engineering context where relevant.

Closed-loop beta.6 evidence uses schema 9.

## UX direction

- one compact engineering workspace without decorative animation as an authority signal;
- relay lamps/LCD/timing rail present state but never become protection logic inputs;
- source configured setpoints must remain distinguishable from effective OUTPUT ON/OFF state;
- `OUTPUT OFF · FROZEN CAPTURE`, generic pickup, TESTSET accepted inputs, operated-element timing, and trip latch must use explicit labels;
- READY TO RE-ARM may only be displayed after the reset transaction's modeled postcondition is satisfied.

## Distribution requirements

Official Windows releases publish a non-elevated per-user installer, single-file portable EXE, portable ZIP, legal notices, SHA-256 checksums, dependency evidence, and release-workflow supply-chain evidence. The selected GitHub Release remains the asset source of truth.

## Current acceptance baseline

The beta.6 feature baseline passed 403 deterministic tests plus .NET CI, CodeQL, cross-platform protection-core checks, Windows packaging, no-admin/single-file contract checks, dependency audit, release asset verification, provenance, and SBOM attestation.

Future stronger fidelity claims require measured device-specific evidence; they cannot be inferred from the generic behavioral model.
