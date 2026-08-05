# Public-site quality assurance

ARVREL treats the public site as a tested product surface rather than a collection of unchecked static files.

## Local checks

```bash
python scripts/prepare-public-site.py
python scripts/validate-public-site.py
python scripts/validate-public-seo.py
python scripts/validate-public-quality.py
npm install --no-audit --no-fund
npx playwright install chromium
npm run test:site
npm run lighthouse
```

`prepare-public-site.py` normalizes Open Graph and Twitter metadata from each page's committed title, description, and canonical URL. The operation is idempotent and runs before validation, browser testing, Lighthouse, and Pages deployment.

## Automated browser matrix

Playwright covers the main product, documentation, research, workflow, safety, download, and 404 routes at:

- 1280 × 800 desktop;
- 768 × 1024 tablet;
- 390 × 844 touch mobile.

Each route must have one `h1`, one `main` landmark, no horizontal overflow, no browser-console or page errors, crawl-safe metadata, usable links, and no automated WCAG 2.x A/AA violations reported by axe-core. Full-page screenshots are attached to the workflow artifact for visual review.

## Lighthouse budgets

The homepage, documentation, download, and research hub must meet minimum scores of:

- performance: 0.85;
- accessibility: 0.95;
- best practices: 0.95;
- SEO: 0.95.

Reports are uploaded as CI artifacts so regressions can be inspected rather than reduced to a pass/fail number.

## Link integrity

Internal navigation is checked in browser tests and the Python static validators. External links are checked weekly and on manual dispatch with retries so temporary third-party rate limits do not make normal pull requests unreliable.

## Search and social rules

- Every indexable HTML page has one absolute canonical URL.
- Every indexable page receives complete Open Graph and Twitter large-image metadata.
- `404.html` uses `noindex,follow` and is excluded from the sitemap.
- The sitemap lists only canonical, indexable routes and uses only `loc` and accurate `lastmod` fields.
