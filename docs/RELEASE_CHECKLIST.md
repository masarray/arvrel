# ARVREL public release checklist

A release is published only after the applicable gates below are complete. Software CI evidence does not replace calibration, laboratory qualification, conformance, type-test, or commissioning evidence.

The current shipped-state authority is [`CURRENT_STATUS.md`](CURRENT_STATUS.md). Versioned public surfaces must be synchronized before publication.

## Source and provenance

- [ ] public-source provenance review completed;
- [ ] no proprietary capture, SCL, customer, station, credential, or employer-confidential data;
- [ ] GPL-3.0-or-later license present;
- [ ] commercial-licensing notice present;
- [ ] third-party notices and dependency manifest generated;
- [ ] contributor rights are compatible with project licensing;
- [ ] `VERSION`, `RELEASE-NOTES.md`, `CITATION.cff`, trust manifest, README, landing page, download page, and release-status page agree on the release identity.

## Core correctness

- [ ] Release build: zero warnings, zero errors;
- [ ] all protection tests pass;
- [ ] all application/laboratory tests pass;
- [ ] all process-bus and capture tests pass;
- [ ] operated-element attribution remains isolated from generic pickup attribution;
- [ ] short-fault pickup/trip evidence survives UI polling;
- [ ] explicit residual channels and documented calculated fallback are tested;
- [ ] duplicate/out-of-order process-bus frames are rejected before measurement and counted diagnostically;
- [ ] settings identity remains culture-invariant;
- [ ] malformed settings fail safely;
- [ ] trip latch and cause clear only on explicit relay reset authority.

## Closed-loop TESTSET / relay metrology gates

- [ ] TESTSET metrology clock remains monotonic integer microseconds and independent of WPF rendering;
- [ ] default BI profile remains 10 kHz / 100 µs unless the release explicitly changes the documented profile;
- [ ] deglitch and debounce are modeled as distinct input behaviors;
- [ ] relay closed-loop path consumes instantaneous signed terminal samples rather than source-side RMS/phasor shortcuts;
- [ ] clipping, quantization, configured front-end delay, and causal measurement order are regression-tested;
- [ ] stopped-source start is primed from settled pre-fault history rather than an empty rolling window;
- [ ] definite timer first-pickup edge semantics are regression-tested;
- [ ] generic ANY PICKUP / BI2 remains distinct from operated-element pickup/P→T;
- [ ] live relay trip request remains distinct from accepted TESTSET BI1;
- [ ] **BI1 is the sole TESTSET measured-trip / trip-auto-stop authority**;
- [ ] open BO1→BI1 wire regression proves internal relay trip does not produce external TESTSET trip or auto-stop;
- [ ] timeline events remain chronological and carry one run/T0 identity;
- [ ] output auto-stop retains configured setpoints while exposing OUTPUT OFF · FROZEN CAPTURE;
- [ ] frozen-capture frame timing is not falsely equated with BI1 acceptance time;
- [ ] one relay RESET transaction settles stale causal acquisition, clears relay state once, releases BO1/BO2 and BI1/BI2, and reaches READY TO RE-ARM;
- [ ] reset preserves completed TESTSET timing and trip/event evidence;
- [ ] reset does not restart or mutate source output/setpoints;
- [ ] reset settle timeout exposes diagnostic state rather than requiring repeated clicks.

## Transformer Differential gates

- [ ] deterministic 10-scenario self-test passes with expected public baseline;
- [ ] synchronized internal HV/LV injection exercises stable through load, internal fault, REF HV/NGR, and REF LV/NGR;
- [ ] internal injector reuses the authoritative transformer protection runtime rather than a duplicate UI algorithm;
- [ ] independent neutral/NGR evidence is required for REF; calculated phase residual is not silently promoted to neutral CT;
- [ ] CT ratio, polarity, transformer rating, and supported vector-group compensation are covered;
- [ ] H2/H5 and context-gated external-fault/CT-saturation security regressions pass;
- [ ] paired external HV/LV SV path validates identity, synchronization, `smpCnt`, `smpSynch`, frequency, mapping, scaling, and trust.

## AVR / OLTC and MMS gates

- [ ] simulated transformer plant and 17-position OLTC operate deterministically;
- [ ] LOCAL/REMOTE and AUTO/MANUAL virtual authority/interlocks are exercised;
- [ ] MMS browse/read and DataSet behavior are tested;
- [ ] report, GI/integrity, and modeled control paths are tested;
- [ ] modeled SBO/SBOw controls terminate inside the virtual AVR/OLTC process;
- [ ] public documentation does not incorrectly claim that MMS control is absent;
- [ ] public documentation states that MMS provides no physical OLTC motor or primary-equipment authority.

## Operator workflow

- [ ] internal demo starts without external capture hardware;
- [ ] SCL and adapter preferences restore safely;
- [ ] Npcap absence produces a useful error only for live capture;
- [ ] relay settings and CT/VT context load/save correctly;
- [ ] waveform, phasor, LCD, LED, timing rail, event trace, and evidence export remain coherent;
- [ ] timing rail labels generic pickup, operated element, relay trip request, and TESTSET BI acceptance explicitly;
- [ ] one-click reset/re-arm is manually sanity-checked on the packaged desktop build;
- [ ] startup/runtime crash diagnostics are generated when required;
- [ ] 100%, 125%, and 150% display scaling reviewed;
- [ ] minimum and common 1600×900 layouts reviewed.

## Release assets

- [ ] self-contained Windows x64 portable EXE;
- [ ] self-contained Windows x64 portable ZIP;
- [ ] per-user non-elevated Windows installer;
- [ ] SHA-256 checksums;
- [ ] NuGet transitive dependency report;
- [ ] CycloneDX SBOM or documented reason it was not generated;
- [ ] build-info file with ARVREL and ARIEC61850 commits;
- [ ] GPL, commercial, security, support, release, and third-party notices included;
- [ ] clean-machine install, launch, uninstall, and portable smoke test;
- [ ] unsigned-binary limitation disclosed or trusted signature verified;
- [ ] GitHub provenance/SBOM attestations succeed when enabled by the release workflow.

## Live laboratory evidence

- [ ] authorized isolated network confirmed;
- [ ] 50 Hz and applicable 60 Hz profiles exercised when live behavior is part of the release objective;
- [ ] duplicate, out-of-order, gap, stale, quality, SCL mismatch, and recovery scenarios reviewed;
- [ ] representative 4I+4V live stream tested;
- [ ] transformer paired-stream behavior reviewed when applicable;
- [ ] soak-test report attached or beta limitation explicitly stated.

## Public documentation and Pages

- [ ] `docs/CURRENT_STATUS.md` reflects the selected release;
- [ ] README current-release table and architecture text are synchronized;
- [ ] public homepage structured data `softwareVersion` matches `VERSION`;
- [ ] download/release-status pages use the current tag and filenames;
- [ ] capabilities, architecture, quick start, evidence/trust, safety, FAQ, and relevant workflows reflect current authority semantics;
- [ ] historical `P*` milestone documents are clearly treated as point-in-time records rather than release authority;
- [ ] `trust-manifest.json` matches `VERSION`, required assets, engine commit, and output authority;
- [ ] `CITATION.cff` matches version/date and current product scope;
- [ ] sitemap `lastmod` values are updated for materially changed public routes;
- [ ] `python scripts/validate-public-site.py` passes;
- [ ] `python scripts/validate-public-seo.py` passes;
- [ ] Pages CI succeeds before merge.

## Claim boundary

- [ ] release remains prerelease while beta limitations remain;
- [ ] no IEC 61850 conformance claim without accredited evidence;
- [ ] no IEC 60255 type-test or calibration claim;
- [ ] no calibrated relay-test-set or commercial-device-equivalence claim from the behavioral timing profile;
- [ ] no hard-real-time, zero-loss, or protection-grade claim without benchmark/qualification evidence;
- [ ] virtual-output-only boundary is visible in README, release notes, application, and public site;
- [ ] live-network authorization and sensitive-data boundaries remain explicit.
