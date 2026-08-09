# Transformer Differential IED — Public Test Guide

> Current public package: **ARVREL v0.1.0-beta.6**. This guide covers the shipped deterministic self-test, synchronized two-sided internal secondary injection, and paired-SV replay/live paths. See [`CURRENT_STATUS.md`](CURRENT_STATUS.md) for the canonical overall product status.

## First test — deterministic self-test, no hardware required

1. Start ARVREL v0.1.0-beta.6.
2. Open **Transformer Differential · 87T / REF**.
3. Leave the main source on **Internal Demo**; external SV streams are not required.
4. Find **Public test / deterministic self-test**.
5. Press **RUN 10-SCENARIO SELF-TEST**.
6. Expected result:

```text
PASS · 10/10 · transformer-public-beta-v1
```

7. Press **VIEW RESULT** and confirm every row is `PASS`.
8. Press **COPY EVIDENCE** before filing a bug report.

If this deterministic test fails, report the failure before tuning settings. The self-test uses fixed software stimulus and the same transformer protection engine used by the application.

### What the 10-scenario suite covers

- compensated through-current stability;
- internal restrained 87T operation;
- 87T-HS operation;
- H2 inrush blocking;
- H5 overexcitation blocking;
- external-fault security when delayed HV CT distortion appears;
- distorted internal fault remains trippable;
- HV REF operation;
- LV REF operation;
- secure REF blocking when an independent neutral-current input is unavailable.

## Second test — synchronized two-sided internal injection

This is the recommended interactive transformer test before PCAP or live capture.

The internal source provides synchronized:

- HV IA/IB/IC/IN;
- LV IA/IB/IC/IN;
- explicit independent neutral/NGR current for each transformer side.

No external merging unit, PCAP, or Npcap is required.

Recommended sequence:

1. Open **Transformer Differential · 87T / REF** and keep **SOURCE = Internal demo**.
2. Review transformer nameplate, CT ratios/polarity, vector group, and active settings.
3. Open **INJECTION**.
4. Select **Balanced through load** and confirm compensated differential current remains low with no 87T trip.
5. Reset the relay.
6. Select **Internal A fault** and verify the expected 87T/87T-HS behavior and operation evidence.
7. Reset the relay.
8. Select **REF HV / NGR** and verify REF HV operation using explicit HV neutral current.
9. Reset the relay.
10. Select **REF LV / NGR** and verify REF LV operation using explicit LV neutral current.
11. Copy/export evidence before changing engineering settings.

The stable through-load baseline is generated from the active transformer engineering configuration so compensated HV/LV currents cancel appropriately instead of assuming equal raw secondary amperes.

**REF invariant:** calculated phase residual is never silently promoted to independent neutral-CT/NGR evidence.

## Third test — paired-SV PCAP replay

Use this when external stream identity, synchronization, mapping/scaling, or network behavior is part of the test objective.

1. Select **PCAP replay**.
2. Replay an authorized PCAP/PCAPNG containing at least two transformer-side Sampled Values streams.
3. Open the Transformer IED workspace.
4. Select the intended HV and LV streams deliberately.
5. Enter/review transformer nameplate, vector group, CT ratio, polarity, and neutral-input configuration.
6. Review generated engineering evidence before enabling protection authority.
7. Apply the runtime.
8. Verify stream identity, `smpCnt`, `smpSynch`, frequency, mapping/scaling, pairing, and trust before interpreting 87T/REF behavior.
9. Export evidence if the result will be reported or reviewed.

Do not publish customer captures merely to report a defect. Prefer a synthetic or contributor-owned fixture whenever possible.

## Fourth test — Live Npcap

Use live capture only on an isolated, authorized laboratory network.

1. Install Npcap separately under the device owner's policy.
2. Select the intended adapter and start live capture.
3. Confirm two independent transformer-side SV streams are present.
4. Select HV/LV streams deliberately.
5. Verify `smpCnt`, `smpSynch`, frequency, stream identity, mapping/scaling, and trust evidence.
6. Confirm transformer/CT/neutral engineering before interpreting 87T or REF operation.

## Protection scope retained in beta.6

- restrained 87T with generic Is1 / K1 / Is2 / K2 dual-slope semantics;
- 87T-HS unrestrained high-set;
- REF HV and REF LV with independent neutral/NGR inputs;
- H2 inrush and H5 overexcitation security;
- context-gated external-fault / CT-saturation security;
- transformer rating, CT ratio, polarity, and supported vector-group compensation;
- paired external HV/LV Sampled Values with synchronization/trust evidence.

These are vendor-neutral laboratory behaviors, not a manufacturer-specific relay conformance profile.

## Minimum bug-report information

Include:

- exact ARVREL version/package name;
- Windows version;
- installer or portable build;
- copied deterministic self-test or injection evidence;
- exact steps to reproduce;
- expected result;
- observed result;
- selected transformer engineering configuration;
- for live/replay issues, non-sensitive pairing/trust/evidence details.

For CT-security/REF issues, also include whether the UI showed:

- `CT DISTORTION · NO BLOCK`;
- `EXT FAULT ARMED`;
- `SECURITY HOLD ACTIVE`;
- HV or LV CT-saturation suspicion;
- REF supervision state.

## Safety and confidentiality

ARVREL is virtual-output laboratory software. The self-test and internal injection are deterministic software verification/evaluation, not calibrated secondary injection, relay calibration, IEC 61850 conformance, IEC 60255 type testing, hard-real-time validation, or commissioning acceptance.

Never publish proprietary SCL files, customer packet captures, credentials, station IP plans, or employer-confidential evidence without explicit authorization.
