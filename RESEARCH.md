# ARVREL research and validation guide

ARVREL is a public engineering beta for IEC 61850 Sampled Values analysis, virtual protection evaluation, trust-gated operation, and reviewable evidence.

This document defines what the current source demonstrates, how to reproduce the deterministic baseline, and which claims remain outside the project.

## Current source-grounded processing chain

```text
Live Npcap / PCAP-PCAPNG replay / internal deterministic source
                              ↓
IEC 61850 SV identity, decode, SCL, mapping, scaling, quality
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
virtual trip latch and reviewable operation evidence
```

## Signal-estimation statement

ARVREL currently uses a complete one-cycle, DC-mean-removed, single-bin discrete Fourier estimator at the nominal fundamental frequency.

It does **not** currently claim:

- a full FFT or harmonic spectrum;
- adaptive frequency tracking;
- calibrated phasor accuracy;
- decaying-DC compensation beyond arithmetic-mean removal;
- IEC 60255 measurement or timing type-test performance.

Implementation: [`FundamentalPhasorEstimator`](src/Arvrel.Protection/FeederProtection.cs)

Deterministic baseline: [`FundamentalEstimator_ReturnsRmsMagnitudeAndBalancedPositiveSequence`](tests/Arvrel.Protection.Tests/FeederProtectionTests.cs)

## Trust and continuity statement

ARVREL evaluates `smpCnt` progression before admitting payload samples to measurement buffers.

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

The site validator checks:

- product version against `VERSION`;
- unique scenario identifiers;
- allowed evidence outcomes;
- existence of referenced source and test files;
- presence of every named test method;
- complete sitemap coverage.

Run the protection baseline:

```powershell
dotnet test .\tests\Arvrel.Protection.Tests\Arvrel.Protection.Tests.csproj -c Release
```

## Current comparison boundary

ARVREL now answers the core functional question:

> Can a software system subscribe to IEC 61850 Sampled Values, estimate protection quantities, execute protection functions, apply trust policy, and preserve the virtual-operation evidence?

For the current public beta, the answer is **yes** for the implemented feeder-protection scope.

ARVREL does **not** currently claim:

- transformer differential 87T;
- CPU-isolated execution;
- deterministic real-time scheduling;
- production virtual-IED availability;
- operational GOOSE or physical trip output;
- calibrated, type-tested, or conformance-certified performance.

Related public route: [Related work and virtual-relay positioning](https://masarray.github.io/arvrel/research/related-work.html)

## 87T research entry criteria

A credible 87T implementation requires more than a protection label:

1. multiple independently trusted and time-aligned SV sources;
2. winding, vector-group, CT-ratio, polarity, and zero-sequence compensation;
3. differential and restraint quantities with percentage-bias characteristics;
4. internal-fault operate and through-fault secure cases;
5. inrush restraint or blocking;
6. CT-saturation security;
7. cross-stream communication-failure policy;
8. settings identity, cause attribution, and reproducible evidence.

See the [public roadmap](https://masarray.github.io/arvrel/roadmap.html#track-87t).

## Related work

- Abdulmueen Alrashide, “Lightweight Virtual Protective Relay,” *2024 IEEE Industry Applications Society Annual Meeting*, pp. 1–6, 2024. DOI: `10.1109/IAS55788.2024.11023755`.
- D. R. Gurusinghe, S. Kariyawasam, and D. S. Ouellette, “Testing of IEC 61850 sampled values based digital substation automation systems,” *The Journal of Engineering*, 2018. DOI: `10.1049/joe.2018.0165`.
- Â. F. Sartori et al., “Performance Analysis of Overcurrent Protection under Corrupted Sampled Value Frames: A Hardware-in-the-Loop Approach,” *Energies*, 16(8), 3386, 2023. DOI: `10.3390/en16083386`.

Inclusion identifies related subject matter. It does not imply reproduced results, endorsement, interoperability, or equivalence.

## Publication and citation

Use the exact ARVREL release or commit, scenario identifiers, settings identity, source description, trust state, and limitations.

Citation metadata: [`CITATION.cff`](CITATION.cff)

Recommended result statement:

> The referenced ARVREL version reproduced the expected software behavior for the stated synthetic fixture. The result is not calibration, IEC 60255 type testing, IEC 61850 conformance, commissioning approval, hard-real-time evidence, or operational authority.

## Public routes

- [Research and validation hub](https://masarray.github.io/arvrel/research/)
- [AN-01 Fundamental signal estimation](https://masarray.github.io/arvrel/research/signal-processing.html)
- [AN-02 SMV continuity and trust](https://masarray.github.io/arvrel/research/smv-continuity.html)
- [AN-03 Directional 67P and 67N](https://masarray.github.io/arvrel/research/directional-protection.html)
- [Deterministic validation matrix](https://masarray.github.io/arvrel/research/validation.html)
- [Laboratory exercises](https://masarray.github.io/arvrel/laboratory-exercises.html)
- [Public roadmap](https://masarray.github.io/arvrel/roadmap.html)
