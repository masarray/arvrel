# Supply-chain security

ARVREL release and CI workflows apply layered controls to reduce dependency and artifact risk.

## Automated controls

- Dependabot checks NuGet packages and GitHub Actions weekly.
- CodeQL scans the C# codebase on pull requests, pushes to `main`, and a weekly schedule.
- CI and release builds use an immutable ARIEC61850 engine commit.
- Every third-party GitHub Action is pinned to a full commit SHA.
- Release builds run with read-only repository access.
- Only the isolated publication job receives `contents: write`, `attestations: write`, and `id-token: write`.
- Release assets receive GitHub build-provenance attestations.
- When a CycloneDX SBOM is available, the installer and portable package also receive SBOM attestations.
- NuGet vulnerability auditing remains a blocking CI and release gate.

## Verify an official release

Download an official installer or portable archive and verify its checksum:

```powershell
Get-FileHash .\ARVREL-Setup-v0.1.0-beta.1-win-x64.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

Verify GitHub build provenance:

```powershell
gh attestation verify .\ARVREL-Setup-v0.1.0-beta.1-win-x64.exe --repo masarray/arvrel
gh attestation verify .\ARVREL-v0.1.0-beta.1-win-x64-portable.zip --repo masarray/arvrel
```

Use the exact versioned file names from the release being checked.

## Trust boundary

Attestations establish which GitHub repository, commit, workflow, and build event produced an artifact. They do not make ARVREL a certified protection IED, prove IEC 61850 conformance, provide IEC 60255 type-test evidence, or authorize operational deployment.
