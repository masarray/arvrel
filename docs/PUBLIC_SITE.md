# Public Site and SEO Maintenance

ARVREL publishes a static public product, documentation, research, and validation site from `docs/` through GitHub Pages.

This document defines maintainership rules that keep the site crawlable, technically accurate, synchronized with the selected public release, and reviewable in pull requests.

## Public authority

Canonical public base:

```text
https://masarray.github.io/arvrel/
```

Repository source:

```text
https://github.com/masarray/arvrel
```

GitHub Releases is the package source of truth. The public site may explain and link to a release, but it must not invent an asset, version, signing state, SBOM state, engine commit, calibration status, timing accuracy, or certification claim.

For current shipped behavior, public maintainers must also use [`CURRENT_STATUS.md`](CURRENT_STATUS.md) as the canonical human-readable status summary.

## Documentation authority and historical records

Use this order when resolving conflicting documentation:

1. selected GitHub Release and release assets;
2. `VERSION` and `RELEASE-NOTES.md`;
3. `docs/CURRENT_STATUS.md`;
4. README, User Guide, architecture/capability/trust/safety public pages;
5. current source and regression tests for implementation detail;
6. historical `P*` milestone documents.

`P0_*`, `P1_*`, and other `P*` documents are intentionally retained as point-in-time engineering records. Do not silently rewrite history to look current. Instead, keep current public surfaces synchronized and explicitly label milestone documents as historical when they are linked from current documentation.

## Information architecture

Primary public routes:

- product homepage: `/`
- capabilities: `/capabilities.html`
- workflow router: `/workflows/`
- documentation hub: `/documentation.html`
- research and validation: `/research/`
- evidence and trust: `/evidence-and-trust.html`
- safety and limitations: `/safety-and-limitations.html`
- quick start: `/quick-start.html`
- engineering FAQ: `/faq.html`
- download and verification: `/download.html`
- release status: `/release-status.html`
- roadmap: `/roadmap.html`

Every public HTML page must be reachable from another public page. New routes must be added to `docs/sitemap.xml`.

## Release synchronization gate

Before a new release is published—or immediately after an emergency release repair—the following surfaces must agree:

- `VERSION`;
- `RELEASE-NOTES.md`;
- `CITATION.cff` version and release date;
- `docs/trust-manifest.json` version, releaseTag, releaseSource, required assets, pinned engine, and authority boundary;
- README public-release table and download links;
- homepage visible version and `SoftwareApplication.softwareVersion` structured data;
- `/download.html` tag and asset filenames;
- `/release-status.html` tag, asset list, release summary, and supply-chain status;
- `/quick-start.html`, `/capabilities.html`, `/architecture.html`, `/evidence-and-trust.html`, `/safety-and-limitations.html`, and `/faq.html` when release semantics changed;
- sitemap `lastmod` for materially updated pages.

A version bump is incomplete while any of these surfaces still advertises an earlier release.

## Product-claim synchronization gate

When a release changes architecture or authority semantics, update all affected public surfaces in the same documentation PR. Examples include:

- what owns measured trip timing;
- whether pickup output is generic or element-specific;
- which clock domain owns a timestamp;
- how auto-stop and frozen capture are related;
- reset/re-arm authority;
- whether MMS control exists and what it may control;
- whether transformer internal injection requires external SV;
- calibration, device-equivalence, or certification boundaries.

Do not fix one landing-page sentence while leaving contradictory user, architecture, FAQ, or trust pages live.

## Canonical URLs

Every HTML page must declare exactly one canonical URL matching its deployed GitHub Pages route.

```html
<link rel="canonical" href="https://masarray.github.io/arvrel/">
<link rel="canonical" href="https://masarray.github.io/arvrel/documentation.html">
<link rel="canonical" href="https://masarray.github.io/arvrel/research/">
```

Do not use repository, raw-content, pull-request, or temporary URLs as public canonicals.

## Sitemap and robots

`docs/sitemap.xml` contains only public canonical HTML routes. Use absolute URLs and an ISO `YYYY-MM-DD` `lastmod` date that reflects meaningful content updates.

`docs/robots.txt` must remain crawlable and advertise:

```text
User-agent: *
Allow: /

Sitemap: https://masarray.github.io/arvrel/sitemap.xml
```

## Metadata requirements

Every HTML page must include:

- a unique descriptive `<title>`;
- concise meta description;
- viewport metadata;
- `lang="en"`;
- exactly one `<h1>`;
- exactly one `<main>`;
- canonical URL;
- stylesheet;
- alt text for content images;
- no `noindex` directive.

The homepage additionally requires Open Graph, Twitter card, same-origin screenshot metadata, `WebSite` structured data, and `SoftwareApplication` structured data.

## Software structured data

The homepage software entity must remain aligned with repository source of truth:

- name: ARVREL;
- public version: exact value from `VERSION`;
- Windows operating system;
- canonical product URL;
- selected release/download URL;
- documentation help URL;
- GPL-3.0 license;
- free offer with price `0` and a currency;
- only currently implemented feature claims;
- no rating, review count, certification, calibration, accuracy, or availability claim without evidence.

When the release changes materially, update `featureList` to describe the current engineering differentiators rather than preserving an obsolete release headline.

## Current beta.6 claim examples

The public site may state that beta.6 implements:

- a deterministic closed-loop virtual TESTSET↔relay path;
- 1 µs metrology-clock resolution in the behavioral software model;
- 10 kHz TESTSET binary-input sampling with separate deglitch/debounce;
- causal relay acquisition from instantaneous signed terminal samples;
- accepted TESTSET BI1 as external measured-trip/auto-stop authority;
- generic ANY PICKUP on TESTSET BI2, separate from operated-element pickup;
- one-click reset/re-arm transaction preserving completed evidence;
- synchronized two-sided Transformer Differential internal injection;
- virtual AVR/OLTC MMS browse/read/report/control behavior.

The same pages must also state that these are behavioral virtual-laboratory capabilities, not calibrated test-equipment timing, manufacturer-specific hardware equivalence, IEC 61850 conformance, IEC 60255 type testing, commissioning acceptance, or switching authority.

## Social previews

Use the canonical same-origin image:

```text
https://masarray.github.io/arvrel/assets/arvrel-main.webp
```

Declared dimensions must match the actual image:

```text
2258 × 1339
```

## Content rules

Public copy must:

- lead with the engineering outcome;
- use terms engineers actually search for, including IEC 61850 Sampled Values, process bus, virtual protection relay, relay testing, secondary injection, feeder protection, transformer differential, AVR/OLTC, PCAP replay, phasor analysis, trust, and engineering evidence;
- distinguish shipped release capabilities from unreleased `main` development;
- distinguish relay internal state from external TESTSET measurement where relevant;
- distinguish virtual MMS control from primary-equipment authority;
- connect claims to exact documentation, source, tests, release metadata, or stated boundaries;
- avoid generic superlatives, certification implication, calibrated-accuracy implication, and unsupported comparison claims;
- state virtual-output, calibration, type-test, conformance, hard-real-time, and switching-authority boundaries where relevant.

## Automated validation

Run locally from repository root:

```powershell
python scripts/validate-public-site.py
python scripts/validate-public-seo.py
```

The public-site validator checks canonical routes, metadata, page structure, links/fragments, duplicate IDs, image metadata, sitemap coverage, robots discovery, trust manifest, citation metadata, research anchors, and deterministic scenarios.

The SEO validator checks required routes, indexability, social metadata, same-origin preview image, valid JSON-LD, `WebSite`/`SoftwareApplication`, free `Offer`, version consistency, key README links, and sitemap metadata.

## Pull-request workflow

Changes under `docs/`, site validators, brand assets, or Pages workflow trigger public-site validation. Pull requests validate but do not deploy; deployment occurs after merge to `main`.

A public-site PR should state:

- what release/product semantics changed;
- which stale or contradictory surfaces were found;
- what is intentionally preserved as historical documentation;
- whether routes changed;
- whether sitemap/metadata/structured data changed;
- which automated checks passed;
- any manual browser/accessibility checks still required.

## Post-merge checks

1. Open homepage, download, release status, quick start, capabilities, architecture, trust, safety, FAQ, and documentation hub.
2. Confirm visible version and current-release links.
3. Confirm canonical URLs in page source.
4. Confirm `robots.txt` and `sitemap.xml` resolve publicly.
5. Inspect homepage JSON-LD and social metadata.
6. Confirm trust manifest version/assets/engine identity.
7. Review Pages deployment logs.
8. Refresh sitemap/indexing tools as appropriate.

Search engines may require time to recrawl and re-evaluate a deployment.

## External references

- Google Search Central: sitemaps — https://developers.google.com/search/docs/crawling-indexing/sitemaps/overview
- Google Search Central: canonical URLs — https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls
- Google Search Central: SoftwareApplication structured data — https://developers.google.com/search/docs/appearance/structured-data/software-app
- Schema.org: SoftwareApplication — https://schema.org/SoftwareApplication
