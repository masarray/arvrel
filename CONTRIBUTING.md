# Contributing to ARVREL

Thank you for helping improve ARVREL. The project values technically rigorous, reviewable contributions that preserve protection correctness, evidence integrity, equipment-authority separation, and the virtual-output safety boundary.

## Before starting

For non-trivial work, open an issue first and describe:

- the engineering problem;
- the expected operator behaviour;
- the IEC 61850, TESTSET/relay, transformer, AVR/OLTC, or protection context;
- the intended tests and evidence;
- any compatibility, timing-authority, or safety impact.

Small documentation corrections may be submitted directly.

## Development baseline

Supported Windows source layout:

```text
Git/
  ARIEC61850/
  arvrel/
```

The selected public beta pins the sibling ARIEC61850 engine through the release workflow. Reproduce that pin when validating a release-specific defect.

Build and test on Windows:

```powershell
cd C:\Git\arvrel
.\scripts\build.cmd
dotnet test .\tests\Arvrel.Protection.Tests\Arvrel.Protection.Tests.csproj -c Release
dotnet test .\tests\Arvrel.Application.Tests\Arvrel.Application.Tests.csproj -c Release
dotnet test .\tests\Arvrel.ProcessBus.Tests\Arvrel.ProcessBus.Tests.csproj -c Release
dotnet test .\tests\Arvrel.Capture.Tests\Arvrel.Capture.Tests.csproj -c Release
```

A pull request must:

- build with zero warnings and zero errors;
- include deterministic regression tests for changed behaviour;
- preserve process-bus trust policy and the virtual-output-only boundary;
- preserve separation between source, relay internals, relay contacts, virtual wiring, TESTSET binary inputs, and WPF presentation where applicable;
- avoid unsupported compliance, calibration, device-equivalence, performance, commissioning, or safety claims;
- document user-visible changes and limitations;
- avoid committing generated binaries, local preferences, captures, secrets, or proprietary SCL data.

## Closed-loop TESTSET / relay changes

Changes affecting feeder internal injection, metrology timing, virtual wiring, relay acquisition, BO contacts, TESTSET BI behavior, auto-stop, frozen capture, or reset/re-arm require dedicated application-layer regression tests.

The following authority rules are non-negotiable unless a future product design explicitly replaces them and updates all tests/docs together:

- TESTSET measured trip and optional trip auto-stop come only from the accepted wired `TESTSET.BI1` edge;
- internal relay `ProtectionSnapshot.TripLatched` may request BO1 but must never become a direct TESTSET result;
- opening BO1→BI1 must permit internal relay trip while preventing external TESTSET trip timing/auto-stop;
- generic `TESTSET.BI2` is ANY PICKUP and must remain distinct from the pickup of the element that ultimately operates;
- operated-element P→T must be correlated to the operated element itself;
- WPF rendering cadence is presentation only, not protection or metrology timing authority;
- relay RESET must remain separate from source authority and preserve completed timing/evidence unless the user explicitly clears stored evidence.

When changing the default behavioral timing/front-end profile, update `docs/CURRENT_STATUS.md`, metrology documentation, public pages, trust manifest, research scenarios, and acceptance tests in the same PR.

## Protection and process-bus changes

Changes affecting protection algorithms, trip attribution, timestamps, quantities, phasors, `smpCnt`, quality, mapping, scaling, trust, transformer compensation, REF neutral provenance, or evidence export require tests covering both intended operation and at least one secure/restrained condition.

Do not use screenshots as the only proof of algorithm correctness.

For Transformer Differential changes, preserve the deterministic public self-test and independent neutral/NGR evidence requirement for REF. For AVR/MMS changes, preserve the simulator-only control boundary and test both accepted and rejected/interlocked commands.

## Documentation and public-site changes

Current public product behavior is governed by the selected GitHub Release, `VERSION`, `RELEASE-NOTES.md`, and `docs/CURRENT_STATUS.md`. Historical `P*` milestone documents are design records and should not be silently rewritten as if they were the current release contract.

When a release or authority semantic changes, synchronize README, citation metadata, trust manifest, current-status/user/architecture docs, affected public Pages routes, roadmap/research claims, sitemap metadata, and issue/reporting surfaces. Run:

```powershell
python scripts/validate-public-site.py
python scripts/validate-public-seo.py
```

## Dual-licensing contribution policy

ARVREL is offered under GPL-3.0-or-later and may also be offered under separately negotiated commercial terms. To preserve this model, code contributions may require acceptance of the project Contributor License Agreement in [`CLA.md`](CLA.md) before merge.

Submitting a pull request does not automatically transfer copyright. The maintainer may decline or postpone a code contribution until the necessary contribution rights are documented. Bug reports, design discussion, test results, and documentation suggestions remain welcome without transferring code ownership.

Do not submit code that you do not have the right to contribute. Clearly identify third-party material and its license.

## Commit and pull-request quality

Use focused commits and describe why the change is necessary. A strong pull request includes:

1. root cause;
2. implementation/authority boundary;
3. regression tests;
4. build/test evidence;
5. operator-visible result;
6. remaining limitations.

## Security

Do not publish suspected vulnerabilities, unsafe defaults, credential exposure, or exploitable parser defects in a public issue. Follow [`SECURITY.md`](SECURITY.md).

## Conduct

Participation is governed by [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md).
