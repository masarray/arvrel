# P15 — Transformer Public Beta Hardening

## Objective

P15 turns the P8–P14 Transformer Differential IED stack into something an external user can verify before connecting live IEC 61850 Sampled Values or opening a field capture.

P15 does **not** add or change a transformer protection algorithm. The authoritative protection behavior remains in `Arvrel.Protection` and the live/replay path remains the P9–P13 process-bus/runtime chain.

## Public-test problem being solved

The transformer runtime needs two independent HV/LV Sampled Values streams. That is correct for real evaluation, but it made the first public test unnecessarily dependent on:

- MU or test-set hardware;
- Npcap and a suitable network adapter;
- a dual-stream PCAP/PCAPNG;
- customer or station data that should not be redistributed merely to reproduce a software installation problem.

P15 therefore adds a deterministic packaged-core self-test that can be run with no SV stream present.

## Architecture

```text
Transformer IED window
        |
        | RUN 10-SCENARIO SELF-TEST
        v
TransformerPublicSelfTest
        |
        v
TransformerProtectionEngine
        |
        +--> 87T / standard Is1-K1-Is2-K2 characteristic
        +--> 87T-HS
        +--> H2 / H5 security
        +--> P13 external-fault / CT-saturation security
        +--> 87N-HV / 87N-LV
        v
copyable PASS / FAIL evidence
```

The WPF layer does not construct a second transformer protection engine, calculate a slope, classify CT saturation, or decide P13 blocking. It only invokes the protection-core self-test and renders the returned report.

## Deterministic public self-test matrix

| ID | Scenario | Required result |
|---|---|---|
| `87T-THROUGH` | Compensated through current | Restrained, no virtual trip |
| `87T-INTERNAL` | Internal phase differential | 87T operates and virtual trip latches |
| `87T-HS` | High-set differential | 87T-HS operates |
| `H2-BLOCK` | 20% second harmonic | Restrained 87T blocked, no trip |
| `H5-BLOCK` | 40% fifth harmonic | Restrained 87T blocked, no trip |
| `P13-EXT-SAT` | Restraint-leading external fault then delayed HV CT distortion | P13 arms, HV security hold blocks false differential, no trip |
| `P13-INTERNAL-DIST` | Distorted internal fault | P13 does not arm/block; 87T still trips |
| `87N-HV` | HV internal earth fault with independent neutral current | 87N-HV operates |
| `87N-LV` | LV internal earth fault with independent neutral current | 87N-LV operates |
| `87N-NO-NEUTRAL` | REF enabled without independent neutral input | REF is securely blocked |

The P13 pair is deliberately included together. A public test that only proves external-fault blocking would be incomplete: the suite must also prove that distorted internal-fault evidence alone does not create an external-fault security block.

## Tester workflow

1. Install the P15 release-candidate package or start the portable build.
2. Open **Transformer differential IED · 87T / REF**. The window may be opened even while the main source remains Internal Demo.
3. In **Public test / deterministic self-test**, press **RUN 10-SCENARIO SELF-TEST**.
4. Confirm `PASS · 10/10`.
5. Press **VIEW RESULT** to inspect every scenario.
6. Press **COPY EVIDENCE** and retain the result together with the ARVREL version and Windows version.
7. Only after the self-test is clean, continue to PCAP replay or Live Npcap if that is part of the evaluation.

A failed deterministic self-test is a software/package defect signal. Do not compensate for a self-test failure by changing transformer settings.

## Live/replay boundary remains unchanged

Opening the workspace no longer requires a discovered SV stream because the self-test does not use the process bus. **Applying the transformer runtime still requires two distinct HV/LV SV streams.** Existing pairing, synchronization, vector-group/CT engineering, harmonic estimation, trust gating and neutral-CT requirements remain authoritative.

The self-test therefore does not weaken the P9/P11 guards.

## Evidence for bug reports

The copied report contains:

- self-test suite ID;
- execution timestamp;
- overall PASS/FAIL and count;
- each scenario ID;
- expected behavior;
- observed protection state;
- authoritative reason/evidence text when available;
- the explicit safety boundary.

A useful external report should also include:

- ARVREL package/version;
- Windows 10/11 build;
- installer or portable package;
- whether Npcap is installed;
- self-test evidence;
- for Live/Replay defects, non-sensitive stream identity and trust evidence.

Never attach customer packet captures, proprietary SCL files, credentials, IP plans or employer-confidential evidence unless redistribution is explicitly authorized.

## What the self-test proves

It is intended to catch packaging/build/regression failures in the shipped transformer protection core and to give every tester the same deterministic first checkpoint.

It does **not** prove:

- IEC 61850 conformance;
- IEC 60255 type-test compliance;
- CT or relay calibration;
- MU/test-set accuracy;
- Ethernet/VLAN/Npcap capture behavior;
- SCL correctness;
- operating-system hard-real-time behavior;
- physical binary output or breaker-trip performance.

## Safety boundary

ARVREL remains virtual-output laboratory software. P15 creates no relay contact, GOOSE trip, MMS control, switching authority or autonomous switching function.

The deterministic self-test must not be presented as a commissioning acceptance certificate or as protection-grade validation of a real substation installation.
