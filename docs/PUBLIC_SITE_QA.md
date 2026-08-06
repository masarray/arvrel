# Public Site Quality Gates

ARVREL treats the public product, documentation, research, and validation site as a tested software surface.

## Quality contract

Every indexable HTML page must provide:

- one non-empty title and meta description;
- `lang="en"`;
- one `h1` and one `main`;
- one canonical URL under the public ARVREL origin;
- one complete Open Graph metadata set;
- one complete Twitter large-image metadata set;
- no `noindex` directive;
- no horizontal overflow at the tested desktop, tablet, and mobile viewports;
- no WCAG A/AA violations reported by Axe in the tested DOM state.

The custom `404.html` is intentionally different:

- it declares `noindex,follow`;
- it has no canonical URL;
- it is excluded from `sitemap.xml`;
- it provides stable recovery routes.

## Local validation

From the repository root:

```powershell
npm ci
npm run site:prepare
npm run site:validate
npm run site:test
npm run site:lighthouse
```

The exact public-site QA dependency graph is committed in `package-lock.json`. Browser tests use the GitHub-hosted Chrome channel rather than downloading a second browser binary.

## Lighthouse budgets

The homepage, documentation hub, download page, and research hub must meet:

| Category | Minimum |
|---|---:|
| Performance | 0.90 |
| Accessibility | 0.95 |
| Best practices | 0.95 |
| SEO | 0.95 |

A budget failure blocks the public-site QA workflow.

## Automated preparation

`scripts/prepare-public-site.py` performs deterministic build-time preparation:

1. stages the canonical icon set from `Asset/icon/`;
2. injects the accessibility enhancement stylesheet;
3. makes horizontally scrollable tables and code regions keyboard focusable;
4. removes stale Open Graph and Twitter tags;
5. recreates one metadata set from each page's committed title, description, and canonical URL;
6. leaves non-indexable social metadata such as `404.html` unchanged.

The script is idempotent and runs before validation, browser testing, Lighthouse, and Pages deployment.

## External links

A weekly and manually dispatched Lychee job audits external links. Temporary third-party rate limits are accepted, but permanent broken links must be corrected or explicitly documented.

## Owner-level repository settings

Settings that cannot be enforced from repository files are recorded in `.github/REPOSITORY_SETTINGS.md`. The repository owner should apply them in GitHub Settings and keep that checklist aligned with the actual repository state.
