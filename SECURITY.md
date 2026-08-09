# Security policy

## Supported versions

Security fixes are provided for the latest published ARVREL beta or stable release and the current `main` branch. Older prereleases may be unsupported.

Current public release: **v0.1.0-beta.6**.

## Reporting a vulnerability

Use GitHub's **private security advisory** workflow for this repository. Do not disclose suspected vulnerabilities in a public issue, pull request, discussion, screenshot, or video before coordinated disclosure.

Include, when available:

- affected version and commit;
- Windows version and architecture;
- reproduction steps or a minimal synthetic fixture;
- expected and observed behaviour;
- security or operational impact;
- crash log from `%LOCALAPPDATA%\ARVREL\logs\arvrel-crash.log`;
- whether closed-loop internal injection, live Npcap, PCAP replay, SCL import, MMS server/control, settings import, or evidence export is involved.

Do not attach customer captures, employer data, substation SCL files, credentials, network plans, device addresses, or other restricted operational information. Replace them with synthetic data.

The maintainer will acknowledge a credible report when practical, assess severity, coordinate a fix, and credit the reporter unless anonymity is requested. Community releases have no guaranteed response-time SLA. Contractual response terms require a separate commercial agreement.

## Operational safety boundary

ARVREL is a laboratory and engineering application. Public beta.6 includes virtual relay outputs and a real laboratory IEC 61850 TCP/MMS endpoint for the simulated AVR/OLTC IED, including modeled controls. These authorities terminate inside the software model.

The public build provides **no**:

- physical relay contact output;
- operational GOOSE trip authority;
- physical OLTC motor-drive output;
- autonomous field switching path;
- primary-equipment control authority.

It does not establish:

- switching authority;
- isolation or interlocking adequacy;
- protection coordination approval;
- functional safety;
- IEC 61850 conformance certification;
- IEC 60255 type-test or calibration status;
- calibrated relay-test-set timing or source accuracy.

Use live process-bus or MMS interoperability features only on isolated, authorized test networks. Never connect an experimental build to an operational process bus/SAS without an approved test plan, independent controls, and responsible asset-owner authorization.

## Closed-loop timing security boundary

The feeder TESTSET↔relay path is intentionally separated into source, virtual wiring, relay front end/protection, relay contacts, and TESTSET binary inputs.

The TESTSET measured trip and optional trip auto-stop authority come only from the accepted wired `TESTSET.BI1` edge. Internal relay `TripLatched` state must never become a shortcut around the virtual BO/contact/wire/BI path.

This behavioral timing model is deterministic software evidence, not traceably calibrated test-equipment metrology.

## Network-control security requirements

The existing MMS controls are constrained to the virtual AVR/OLTC process, expose modeled authority/interlocks, and preserve accepted/rejected control evidence.

Any future network-output feature capable of affecting equipment outside the simulator must remain disabled by default, expose destination and armed state, support dry run, preserve independent evidence, require explicit laboratory arming, and undergo separate security/safety review. Operational network-output authority is outside the scope of beta.6.

## Release provenance and dependency controls

Official release assets use pinned workflow dependencies, blocking vulnerability checks, build-provenance attestations, and SBOM attestations when a CycloneDX SBOM is available. Beta.6 publishes checksums, dependency evidence, CycloneDX SBOM, and provenance attestations.

Verification instructions and the exact trust boundary are documented in [Supply-chain security](docs/SUPPLY_CHAIN_SECURITY.md) and [Release status](https://masarray.github.io/arvrel/release-status.html).
