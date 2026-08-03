# Contributing to ARVREL

Thank you for helping improve ARVREL. The project values technically rigorous, reviewable contributions that preserve protection correctness, evidence integrity, and the virtual-output safety boundary.

## Before starting

For non-trivial work, open an issue first and describe:

- the engineering problem;
- the expected operator behaviour;
- the IEC 61850 or protection context;
- the intended tests and evidence;
- any compatibility or safety impact.

Small documentation corrections may be submitted directly.

## Development baseline

Supported source layout:

```text
Git/
  ARIEC61850/
  arvrel/
```

Build and test on Windows:

```powershell
cd C:\Git\arvrel
.\scripts\build.cmd
dotnet test .\tests\Arvrel.Protection.Tests\Arvrel.Protection.Tests.csproj -c Release
dotnet test .\tests\Arvrel.ProcessBus.Tests\Arvrel.ProcessBus.Tests.csproj -c Release
```

A pull request must:

- build with zero warnings and zero errors;
- include deterministic regression tests for changed behaviour;
- preserve the SMV trust policy and virtual-output-only boundary;
- avoid unsupported compliance, calibration, performance, or safety claims;
- document user-visible changes and limitations;
- avoid committing generated binaries, local preferences, captures, secrets, or proprietary SCL data.

## Protection and process-bus changes

Changes affecting protection algorithms, trip attribution, timestamps, quantities, phasors, `smpCnt`, quality, mapping, scaling, or evidence export require tests that cover both the intended operation and at least one secure/restrained condition.

Do not use screenshots as the only proof of algorithm correctness.

## Dual-licensing contribution policy

ARVREL is offered under GPL-3.0-or-later and may also be offered under separately negotiated commercial terms. To preserve this model, code contributions may require acceptance of the project Contributor License Agreement in [`CLA.md`](CLA.md) before merge.

Submitting a pull request does not automatically transfer copyright. The maintainer may decline or postpone a code contribution until the necessary contribution rights are documented. Bug reports, design discussion, test results, and documentation suggestions remain welcome without transferring code ownership.

Do not submit code that you do not have the right to contribute. Clearly identify third-party material and its license.

## Commit and pull-request quality

Use focused commits and describe why the change is necessary. A strong pull request includes:

1. root cause;
2. implementation boundary;
3. regression tests;
4. build/test evidence;
5. operator-visible result;
6. remaining limitations.

## Security

Do not publish suspected vulnerabilities, unsafe defaults, credential exposure, or exploitable parser defects in a public issue. Follow [`SECURITY.md`](SECURITY.md).

## Conduct

Participation is governed by [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md).
