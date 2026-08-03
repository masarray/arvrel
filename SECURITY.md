# Security policy

## Supported versions

Security fixes are provided for the latest published ARVREL beta or stable release and the current `main` branch. Older prereleases may be unsupported.

## Reporting a vulnerability

Use GitHub's **private security advisory** workflow for this repository. Do not disclose suspected vulnerabilities in a public issue, pull request, discussion, screenshot, or video before coordinated disclosure.

Include, when available:

- affected version and commit;
- Windows version and architecture;
- reproduction steps or a minimal synthetic fixture;
- expected and observed behaviour;
- security or operational impact;
- crash log from `%LOCALAPPDATA%\ARVREL\logs\arvrel-crash.log`;
- whether live Npcap, PCAP replay, SCL import, settings import, or evidence export is involved.

Do not attach customer captures, employer data, substation SCL files, credentials, network plans, device addresses, or other restricted operational information. Replace them with synthetic data.

The maintainer will acknowledge a credible report when practical, assess severity, coordinate a fix, and credit the reporter unless anonymity is requested. Community releases have no guaranteed response-time SLA. Contractual response terms require a separate commercial agreement.

## Operational safety boundary

ARVREL is a laboratory and engineering application. The standard public build has no active GOOSE trip, MMS control, relay contact, or autonomous switching path. It does not establish:

- switching authority;
- isolation or interlocking adequacy;
- protection coordination approval;
- functional safety;
- IEC 61850 conformance certification;
- IEC 60255 type-test or calibration status.

Use live process-bus features only on isolated, authorized test networks. Never connect an experimental build to an operational process bus without an approved test plan, independent controls, and responsible asset-owner authorization.

## Security-sensitive design requirements

Any future active network-output function must remain disabled by default, expose its destination and armed state, support dry run, preserve independent evidence, and require explicit laboratory arming. Such a function is outside the scope of the v0.1.0 public beta.
