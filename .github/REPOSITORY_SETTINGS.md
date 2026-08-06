# Recommended GitHub Repository Settings

These settings are part of ARVREL's professional repository contract but must be applied through GitHub repository settings.

## Repository profile

Recommended description:

> Open-source IEC 61850 Sampled Values virtual protection relay laboratory for process-bus analysis, feeder protection, PCAP replay, phasors, trust gating, and engineering evidence.

Recommended topics:

```text
iec-61850
sampled-values
smv
process-bus
protection-relay
power-system-protection
digital-substation
substation-automation
feeder-protection
pcap
phasor
scl
csharp
dotnet
avalonia
wpf
electrical-engineering
research-software
```

Use the public product site as the repository homepage:

```text
https://masarray.github.io/arvrel/
```

Upload a dedicated 1280×640 repository social preview that combines the ARVREL identity, the IEC 61850 Sampled Values laboratory descriptor, and a legible product screenshot.

## Pull requests and merge policy

Recommended settings:

- allow squash merge;
- disable merge commits;
- disable rebase merges unless a documented release workflow requires them;
- use the pull-request title as the squash commit title;
- automatically delete head branches after merge;
- allow maintainers to update pull-request branches;
- require conversation resolution before merge.

## Main branch ruleset

Protect `main` with a repository ruleset:

- require pull requests;
- require at least one approval when independent reviewers are available;
- dismiss stale approvals after new commits;
- require CODEOWNERS review for security, workflows, release, and protection logic;
- require successful status checks;
- require branches to be up to date before merge;
- block force pushes;
- block branch deletion;
- require linear history;
- restrict bypass to emergency maintainers.

Required checks should include the current names for:

- .NET build and tests;
- CodeQL;
- public-site validation;
- public-site browser/Axe/Lighthouse QA;
- application and process-bus portability;
- Avalonia packaging where applicable;
- desktop release validation.

## Community

Recommended settings:

- enable Discussions with `Q&A`, `Ideas`, `Research`, and `Show and tell`;
- disable Wiki unless it has a maintained purpose distinct from versioned documentation;
- keep Issues enabled and use repository issue templates;
- publish a support policy before promising response-time commitments.

## Release governance

- publish releases only from reviewed version bumps;
- preserve checksums, dependency reports, SBOM, provenance, engine commit, and build metadata;
- do not move an existing release tag;
- make stable releases immutable after verification;
- add commercial code signing only when a sustainable certificate and release process exist.
