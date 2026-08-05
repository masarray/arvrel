# Public Site and SEO Maintenance

ARVREL publishes a static public product, documentation, research, and validation site from `docs/` through GitHub Pages.

This document defines the maintainership rules that keep the site crawlable, technically accurate, internally consistent, and reviewable in pull requests.

## Public authority

Canonical public base:

```text
https://masarray.github.io/arvrel/
```

Repository source:

```text
https://github.com/masarray/arvrel
```

GitHub Releases is the package source of truth. The public site may explain and link to a release, but it must not invent an asset, version, signing state, SBOM state, or engine commit.

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

## Canonical URLs

Every HTML page must declare exactly one canonical URL that matches its deployed GitHub Pages route.

Examples:

```html
<link rel="canonical" href="https://masarray.github.io/arvrel/">
<link rel="canonical" href="https://masarray.github.io/arvrel/documentation.html">
<link rel="canonical" href="https://masarray.github.io/arvrel/research/">
```

Do not mix repository URLs, raw-content URLs, and GitHub Pages URLs as canonicals.

## Sitemap and robots

`docs/sitemap.xml` contains only public canonical HTML routes. Use absolute URLs and an ISO `YYYY-MM-DD` `lastmod` date that reflects a meaningful content update.

`docs/robots.txt` must remain crawlable and advertise the sitemap:

```text
User-agent: *
Allow: /

Sitemap: https://masarray.github.io/arvrel/sitemap.xml
```

A sitemap supports URL discovery and canonical signals; it does not guarantee indexing.

## Metadata

Every HTML page must include:

- a unique, descriptive `<title>`;
- a concise meta description;
- viewport metadata;
- `lang="en"`;
- exactly one `<h1>`;
- exactly one `<main>`;
- a canonical URL;
- a stylesheet;
- alt text for content images;
- no `noindex` directive.

The homepage also includes:

- Open Graph title, description, URL, site name, and image;
- Twitter card, title, description, and image;
- a same-origin public screenshot URL;
- `WebSite` structured data;
- `SoftwareApplication` structured data.

## Software structured data

The homepage software entity must remain aligned with repository source of truth:

- name: ARVREL;
- public version: value from `VERSION`;
- Windows operating system;
- canonical product URL;
- GitHub Releases download URL;
- documentation help URL;
- GPL-3.0 license;
- free offer with price `0` and a currency;
- real implemented feature list;
- no rating, review count, certification, or availability claim without evidence.

Validate structured data syntax before merge. Structured data improves machine understanding but does not guarantee a rich result.

## Social previews

Use the canonical GitHub Pages image:

```text
https://masarray.github.io/arvrel/assets/arvrel-main.webp
```

The declared dimensions must match the actual image dimensions:

```text
2258 × 1339
```

Do not point the primary preview image to a branch-specific, pull-request, temporary, or private URL.

## Content rules

Public copy must:

- lead with the engineering outcome;
- use terms engineers actually search for, including IEC 61850 Sampled Values, process bus, virtual protection relay, feeder protection, PCAP replay, phasor analysis, trust, and engineering evidence;
- distinguish public release capabilities from unreleased `main` development;
- connect claims to exact documentation, source, tests, release metadata, or stated boundaries;
- avoid generic superlatives, certification implication, and unsupported comparison claims;
- state virtual-output, calibration, type-test, conformance, hard-real-time, and switching-authority boundaries where relevant.

## Automated validation

Run locally from the repository root:

```powershell
python scripts/validate-public-site.py
python scripts/validate-public-seo.py
```

The public-site validator checks:

- canonical routes;
- metadata;
- one `<main>` and one `<h1>`;
- local links and fragments;
- duplicate IDs;
- image alt text and native dimensions;
- sitemap coverage;
- robots discovery;
- trust manifest;
- citation metadata;
- research source anchors and deterministic scenarios.

The SEO validator checks:

- required documentation and FAQ routes;
- no public `noindex`;
- homepage Open Graph and Twitter metadata;
- same-origin social image;
- valid JSON-LD;
- `WebSite` and `SoftwareApplication` entities;
- free `Offer` properties required by the project;
- software version consistency with `VERSION`;
- key README public links;
- sitemap `lastmod` format and required routes.

## Pull-request workflow

Changes under `docs/`, the site validators, brand assets, or the Pages workflow trigger the public-site validation job.

Pull requests validate but do not deploy. Deployment occurs only after merge to `main`.

A public-site PR should state:

- what changed;
- why the change improves evaluation, discoverability, trust, or maintainability;
- whether any product claim changed;
- which routes were added or removed;
- whether sitemap and validation were updated;
- which automated checks were run;
- any manual browser, accessibility, structured-data, or Search Console checks still required.

## Post-merge checks

After deployment:

1. open the homepage and every new route;
2. confirm canonical URLs in page source;
3. confirm `robots.txt` and `sitemap.xml` return plain public content;
4. submit or refresh the sitemap in Google Search Console;
5. inspect the homepage and new routes with URL Inspection;
6. test structured data with an appropriate validator;
7. confirm Open Graph and Twitter images resolve publicly;
8. review Pages deployment logs;
9. record indexing issues as repository issues without weakening safety or technical boundaries.

Search engines may require time to recrawl and re-evaluate a deployment.

## External references

- Google Search Central: sitemaps
  - https://developers.google.com/search/docs/crawling-indexing/sitemaps/overview
  - https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap
- Google Search Central: canonical URLs
  - https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls
- Google Search Central: SoftwareApplication structured data
  - https://developers.google.com/search/docs/appearance/structured-data/software-app
- Schema.org: SoftwareApplication
  - https://schema.org/SoftwareApplication
