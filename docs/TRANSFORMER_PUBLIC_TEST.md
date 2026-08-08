# Transformer Differential IED — Public Test Guide

This guide is the shortest supported path for evaluating the ARVREL Transformer Differential IED public release candidate.

## First test — no hardware required

1. Start ARVREL.
2. Open the **Transformer differential IED · 87T / REF** toolbar entry.
3. You may leave the main application on **Internal Demo**. Two SV streams are **not** required for this first check.
4. Find **Public test / deterministic self-test**.
5. Press **RUN 10-SCENARIO SELF-TEST**.
6. The expected result is:

```text
PASS · 10/10 · transformer-public-beta-v1
```

7. Press **VIEW RESULT** and confirm every row is `PASS`.
8. Press **COPY EVIDENCE** before filing a bug report.

If this deterministic test fails, report the failure before attempting to tune settings. The self-test uses fixed software stimulus and the same transformer protection engine used by the application.

## What is covered

The 10-scenario suite verifies:

- compensated through-fault stability;
- internal restrained 87T operation;
- 87T-HS operation;
- H2 inrush blocking;
- H5 overexcitation blocking;
- external-fault security when delayed HV CT distortion appears;
- the critical opposite case: distorted internal fault must remain trippable;
- HV REF operation;
- LV REF operation;
- secure REF blocking when an independent neutral-current input is unavailable.

## Second test — PCAP replay

Use this only after the deterministic test passes.

1. Select **PCAP replay** in the main workspace.
2. Replay an authorized PCAP/PCAPNG containing at least two transformer-side Sampled Values streams.
3. Open the Transformer IED workspace.
4. Select the intended HV and LV streams.
5. Enter the transformer nameplate, vector group and CT data.
6. Review the generated engineering evidence before enabling protection.
7. Apply the runtime.
8. Verify pairing/synchronization/trust before interpreting 87T behavior.
9. Export evidence if the result is being reported or reviewed.

Do not publish customer captures merely to report a defect. Prefer a synthetic or contributor-owned fixture whenever possible.

## Third test — Live Npcap

Use live capture only on an isolated, authorized laboratory network.

1. Install Npcap separately.
2. Select the intended adapter and start Live capture.
3. Confirm two independent transformer-side SV streams are present.
4. Open the Transformer IED workspace and select HV/LV streams deliberately.
5. Verify `smpCnt`, `smpSynch`, frequency, stream identity, mapping/scaling and trust evidence.
6. Confirm transformer/CT engineering before enabling 87T or REF.

## Minimum bug-report information

Include:

- ARVREL version/package name;
- Windows version;
- installer or portable build;
- copied deterministic self-test evidence;
- exact steps to reproduce;
- expected result;
- observed result;
- for Live/Replay issues, non-sensitive pairing/trust/evidence details.

For a P13 issue, include whether the UI showed:

- `CT DISTORTION · NO BLOCK`;
- `EXT FAULT ARMED`;
- `SECURITY HOLD ACTIVE`;
- HV or LV CT-saturation suspicion;
- REF supervision state.

## Safety and confidentiality

ARVREL is virtual-output laboratory software. The self-test is deterministic software verification, not relay calibration, IEC 61850 conformance, IEC 60255 type testing, hard-real-time validation or commissioning acceptance.

Never publish proprietary SCL files, customer packet captures, credentials, station IP plans or employer-confidential evidence without explicit authorization.
