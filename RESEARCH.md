# ARVREL research and validation guide

> Current public baseline: **ARVREL v0.1.0-beta.6**. See [`docs/CURRENT_STATUS.md`](docs/CURRENT_STATUS.md) for the canonical shipped-state summary.

ARVREL is a public engineering beta for virtual relay testing, IEC 61850 Sampled Values analysis, feeder and transformer protection, AVR/OLTC simulation, laboratory MMS interoperability, trust-gated operation, and reviewable evidence.

This document defines what the current source demonstrates, how to reproduce deterministic baselines, and which stronger claims remain outside the project.

## Current source-grounded processing chains

### Closed-loop feeder secondary injection

```text
TESTSET metrology T0
        ↓
instantaneous 4I+4V source
        ↓
virtual analog wiring
        ↓
causal relay front end
(clipping / quantization / delay / rolling DFT)
        ↓
feeder protection + timers
        ↓
relay BO contact behavior
        ↓
virtual binary wiring
        ↓
independent TESTSET BI sampling / deglitch / debounce
        ↓
accepted BI edge
        ↓
external measured timing / optional auto-stop
```

The accepted wired `TESTSET.BI1` edge owns the external measured trip. Internal `ProtectionSnapshot.TripLatched` may request BO1 but is never read directly as the TESTSET result.

Default beta.6 behavioral profile:

- monotonic TESTSET clock: 1 µs resolution;
- TESTSET BI: 10 kHz / 100 µs;
- BI deglitch: 0.5 ms;
- BI debounce holdoff: 0 ms;
- relay processing/acquisition: 4 kHz / 250 µs;
- behavioral relay input/filter delay: 1.5 ms;
- 16-bit-equivalent ADC, 20 A RMS current full scale, 300 V RMS voltage full scale;
- one nominal 50 Hz causal rolling DFT with settled pre-fault history.

This is deterministic behavioral software, not calibrated relay-test equipment or a named commercial relay/test-set clone.

### IEC 61850 process bus

```text
Live Npcap / PCAP-PCAPNG replay
        ↓
IEC 61850 SV identity / decode / SCL / mapping / scaling / quality
        ↓
smpCnt continuity and payload-admission decisions
        ↓
complete one-cycle measurement window
        ↓
mean removal + nominal-frequency single-bin DFT
        ↓
complex RMS phase / residual / sequence phasors
        ↓
50 / 51 / 50N / 51N / 67P / 67N / 27 / 59 / 59N
        ↓
AllowsMeasurement / AllowsPickup / AllowsTrip
        ↓
virtual trip latch and reviewable evidence
```

### Transformer Differential

```text
internal synchronized HV/LV/independent-neutral source
                         ┐
                         ├─> transformer engineering compensation
paired external HV/LV SV ┘          ↓
                            87T / 87T-HS / REF HV / REF LV
                                      ↓
                              security + trust + evidence
```

The shipped Transformer runtime includes CT ratio/polarity, transformer rating, supported vector-group compensation, H2/H5 security, context-gated external-fault/CT-saturation behavior, deterministic 10-scenario self-test, synchronized two-sided internal injection, and paired external-SV evaluation. Calculated phase residual is not silently promoted to independent neutral/NGR evidence for REF.

### AVR / OLTC and MMS

```text
virtual transformer plant
        ↕
AVR / 17-position OLTC logic
        ↕
LOCAL/REMOTE + AUTO/MANUAL + interlocks
        ↕
laboratory IEC 61850 MMS model
```

MMS browse/read, DataSets, reports, GI/integrity, modeled SBO/SBOw controls, and virtual AVR settings exist in beta.6. Accepted controls terminate inside the simulator and provide no physical OLTC motor or primary-equipment authority.

## Signal-estimation statement

The process-bus feeder path currently uses a complete one-cycle, DC-mean-removed, single-bin discrete Fourier estimator at the nominal fundamental frequency.

It does **not** claim:

- a full FFT or harmonic spectrum;
- adaptive frequency tracking;
- calibrated phasor accuracy;
- decaying-DC compensation beyond arithmetic-mean removal;
- IEC 60255 measurement or timing type-test performance.

Implementation: [`FundamentalPhasorEstimator`](src/Arvrel.Protection/FeederProtection.cs)

Deterministic baseline: [`FundamentalEstimator_ReturnsRmsMagnitudeAndBalancedPositiveSequence`](tests/Arvrel.Protection.Tests/FeederProtectionTests.cs)

The closed-loop feeder relay front end is a separate causal path that consumes instantaneous signed terminal samples before producing its rolling fundamental measurement.

## Trust and continuity statement

ARVREL evaluates `smpCnt` progression before admitting external SV payload samples to measurement buffers.

- expected next counter: accept;
- duplicate counter: reject payload;
- out-of-order counter: reject payload;
- forward discontinuity: restart contiguous measurement windows and enter recovery evidence;
- communication discontinuity does not silently clear an existing trip latch;
- diagnostic pickup can remain visible while current trust blocks a new virtual trip.

Implementation:

- [`SmvIngressContinuityGate`](src/Arvrel.ProcessBus/SmvIngressContinuityGate.cs)
- [`SmvProcessBusController`](src/Arvrel.ProcessBus/SmvProcessBusController.cs)
- [`ProtectionEngine`](src/Arvrel.Protection/ProtectionEngine.cs)

Deterministic baseline: [`SmvTrustGate_BlocksOperationWithoutHidingPickup`](tests/Arvrel.Protection.Tests/ProtectionEngineTests.cs)

## Closed-loop timing semantics

Beta.6 deliberately separates:

- `RELAY ANY PU [source]` — first generic protection pickup requesting BO2;
- `TESTSET BI2 ACCEPT` — accepted wired generic ANY-PICKUP contact edge;
- operated-element pickup — pickup of the element that ultimately operates;
- operated-element P→T — that element's own pickup-to-trip interval;
- relay trip request — live relay trip-latch rising edge requesting BO1;
- `TESTSET BI1 ACCEPT` — authoritative external trip time and optional auto-stop trigger.

BI2 may legitimately precede the eventual operated-element pickup. A representable 60 ms 50P definite-time setting produces exactly **60.000 ms** operated-element P→T in the reference engine.

The open-trip-wire regression is a required negative case: disconnecting BO1→BI1 allows internal relay trip while preventing external TESTSET trip measurement and trip auto-stop.

## Frozen capture and reset/re-arm statement

After BI1 auto-stop, beta.6 exposes **OUTPUT OFF · FROZEN CAPTURE**. Configured setpoints remain for repeatability while effective output is zero.

One deterministic relay RESET transaction advances stale causal acquisition out, clears relay latch/timers once, releases BO1/BO2 and TESTSET BI1/BI2, and reports **READY TO RE-ARM** only after the full postcondition is satisfied. Completed TESTSET timing and frozen trip/event evidence are retained. Relay RESET does not restart or mutate the source.

Closed-loop exported evidence is schema **9**.

## Directional protection statement

- **67P:** positive-sequence current `I1` with positive-sequence voltage `V1` polarization;
- **67N:** residual current `3I0` with residual voltage `3V0` polarization;
- minimum polarizing voltage supervision is required;
- selected direction is based on the sign of a cosine torque expression relative to the configured characteristic angle;
- forward operate is paired with reverse restraint in the deterministic baseline;
- explicit residual channels are preferred over phase-sum fallback when available.

Implementation: [`FeederProtectionEngine`](src/Arvrel.Protection/FeederProtection.cs)

Deterministic baseline: [`FeederProtectionTests`](tests/Arvrel.Protection.Tests/FeederProtectionTests.cs)

## Machine-readable deterministic scenarios

Public scenario catalog:

- [`docs/data/research-scenarios.json`](docs/data/research-scenarios.json)
- [Public validation matrix](https://masarray.github.io/arvrel/research/validation.html)

The beta.6 scenario catalog includes foundational signal/protection/trust cases plus metrology scenarios for:

- the 1 µs / 10 kHz TESTSET profile;
- settled pre-fault causal relay acquisition;
- desktop A-B-G timing correlation;
- chronological metrology event order;
- open BO1→BI1 external-trip restraint.

The site validator checks product version, unique scenario IDs, allowed evidence outcomes, referenced source/test files, named test methods, and sitemap coverage.

Run deterministic baselines:

```powershell
dotnet test .\tests\Arvrel.Protection.Tests\Arvrel.Protection.Tests.csproj -c Release
dotnet test .\tests\Arvrel.Application.Tests\Arvrel.Application.Tests.csproj -c Release
```

## Current comparison boundary

ARVREL now answers several functional questions with shipped software:

- can a software test-set/relay loop keep external binary-input timing separate from relay internal trip state? **Yes, within the documented behavioral model.**
- can software subscribe to IEC 61850 Sampled Values, estimate protection quantities, apply trust policy, and preserve operation evidence? **Yes, within the implemented feeder scope.**
- can one virtual IED evaluate two-winding Transformer Differential/REF with internal and paired-SV inputs? **Yes, within the documented vendor-neutral transformer model.**
- can a virtual AVR/OLTC expose laboratory MMS browse/report/control behavior? **Yes, with commands terminating inside the simulator.**

ARVREL does **not** claim:

- calibrated secondary-injection or TESTSET uncertainty;
- manufacturer-specific relay/test-set hardware equivalence;
- CPU-isolated production execution;
- deterministic real-time scheduling or protection-grade hard-real-time behavior;
- IEC 61850 conformance certification;
- IEC 60255 type-test status;
- operational GOOSE, physical trip, OLTC motor, switching, or primary-equipment authority;
- commissioning acceptance.

Related public route: [Related work and virtual-relay positioning](https://masarray.github.io/arvrel/research/related-work.html)

## Future research entry criteria

The next major evidence tracks are not new labels for already shipped 87T or MMS. They require measured or independently instrumented evidence:

1. measured relay/test-set anti-alias/input transfer behavior;
2. externally observable ADC behavior, channel skew, and input burden;
3. binary-input electrical thresholds and timing;
4. measured/statistical relay contact behavior;
5. calibrated timing uncertainty and reference instrumentation;
6. broader estimator characterization across frequency, harmonics, noise, decaying DC, and CT saturation;
7. expanded transformer vector groups and measured CT behavior;
8. deterministic-host timing contracts and independent HIL comparison.

See the [public roadmap](https://masarray.github.io/arvrel/roadmap.html).

## Related work

- Abdulmueen Alrashide, “Lightweight Virtual Protective Relay,” *2024 IEEE Industry Applications Society Annual Meeting*, pp. 1–6, 2024. DOI: `10.1109/IAS55788.2024.11023755`.
- D. R. Gurusinghe, S. Kariyawasam, and D. S. Ouellette, “Testing of IEC 61850 sampled values based digital substation automation systems,” *The Journal of Engineering*, 2018. DOI: `10.1049/joe.2018.0165`.
- Â. F. Sartori et al., “Performance Analysis of Overcurrent Protection under Corrupted Sampled Value Frames: A Hardware-in-the-Loop Approach,” *Energies*, 16(8), 3386, 2023. DOI: `10.3390/en16083386`.

Inclusion identifies related subject matter. It does not imply reproduced results, endorsement, interoperability, or equivalence.

## Publication and citation

Use the exact ARVREL release or commit, scenario identifiers, settings identity, source description, virtual wiring/topology when relevant, timing/trust profile, and limitations.

Citation metadata: [`CITATION.cff`](CITATION.cff)

Recommended result statement:

> The referenced ARVREL version reproduced the expected deterministic software behavior for the stated fixture. The result is not calibration, IEC 60255 type testing, IEC 61850 conformance, commissioning approval, hard-real-time evidence, or operational authority.

## Public routes

- [Current shipped status](docs/CURRENT_STATUS.md)
- [Research and validation hub](https://masarray.github.io/arvrel/research/)
- [AN-01 Fundamental signal estimation](https://masarray.github.io/arvrel/research/signal-processing.html)
- [AN-02 SMV continuity and trust](https://masarray.github.io/arvrel/research/smv-continuity.html)
- [AN-03 Directional 67P and 67N](https://masarray.github.io/arvrel/research/directional-protection.html)
- [Deterministic validation matrix](https://masarray.github.io/arvrel/research/validation.html)
- [Laboratory exercises](https://masarray.github.io/arvrel/laboratory-exercises.html)
- [Public roadmap](https://masarray.github.io/arvrel/roadmap.html)
