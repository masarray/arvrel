import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(currentDirectory, '../..');
const sitemap = fs.readFileSync(path.join(repositoryRoot, 'docs', 'sitemap.xml'), 'utf8');
const publicBasePath = '/arvrel';

const routes = [...sitemap.matchAll(/<loc>https:\/\/masarray\.github\.io\/arvrel\/([^<]*)<\/loc>/g)]
  .map((match) => `/${match[1]}`)
  .map((route) => route.replace(/\/index\.html$/, '/'))
  .map((route) => (route === '//' ? '/' : route));

routes.unshift('/');
routes.push('/404.html');

const uniqueRoutes = [...new Set(routes)];

for (const route of uniqueRoutes) {
  test(`${route} renders accessibly with valid public metadata`, async ({ page }) => {
    const runtimeErrors = [];
    page.on('pageerror', (error) => runtimeErrors.push(error.message));
    page.on('console', (message) => {
      if (message.type() === 'error') {
        runtimeErrors.push(message.text());
      }
    });

    const response = await page.goto(route, { waitUntil: 'domcontentloaded' });
    expect(response, `No HTTP response for ${route}`).not.toBeNull();
    expect(response.status(), `Unexpected status for ${route}`).toBeLessThan(400);

    const snapshot = await page.evaluate(() => {
      const canonical = [...document.querySelectorAll('link[rel~="canonical"]')];
      const robots = document.querySelector('meta[name="robots"]')?.content.toLowerCase() ?? '';
      const invalidLinks = [...document.querySelectorAll('a[href]')]
        .map((link) => link.getAttribute('href') ?? '')
        .filter((href) => !href.trim() || href.trim().toLowerCase().startsWith('javascript:'));

      return {
        language: document.documentElement.lang.toLowerCase(),
        h1Count: document.querySelectorAll('h1').length,
        mainCount: document.querySelectorAll('main').length,
        canonicalCount: canonical.length,
        canonical: canonical[0]?.href ?? '',
        robots,
        invalidLinks,
        overflow: Math.max(
          document.documentElement.scrollWidth,
          document.body?.scrollWidth ?? 0,
        ) - window.innerWidth,
        ogUrlCount: document.querySelectorAll('meta[property="og:url"]').length,
        ogImageCount: document.querySelectorAll('meta[property="og:image"]').length,
        twitterCard: document.querySelector('meta[name="twitter:card"]')?.content ?? '',
      };
    });

    expect(snapshot.language).toBe('en');
    expect(snapshot.h1Count).toBe(1);
    expect(snapshot.mainCount).toBe(1);
    expect(snapshot.invalidLinks).toEqual([]);
    expect(snapshot.overflow).toBeLessThanOrEqual(2);

    if (route === '/404.html') {
      expect(snapshot.robots).toContain('noindex');
      expect(snapshot.robots).toContain('follow');
      expect(snapshot.canonicalCount).toBe(0);
    } else {
      expect(snapshot.robots).not.toContain('noindex');
      expect(snapshot.canonicalCount).toBe(1);
      expect(snapshot.canonical).toContain(publicBasePath);
      expect(snapshot.ogUrlCount).toBe(1);
      expect(snapshot.ogImageCount).toBe(1);
      expect(snapshot.twitterCard).toBe('summary_large_image');
    }

    const accessibility = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
      .analyze();

    expect(
      accessibility.violations,
      JSON.stringify(accessibility.violations, null, 2),
    ).toEqual([]);
    expect(runtimeErrors).toEqual([]);
  });
}
