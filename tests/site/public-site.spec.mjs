import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const routes = [
  ['home', '/'],
  ['documentation', '/documentation.html'],
  ['download', '/download.html'],
  ['quick-start', '/quick-start.html'],
  ['capabilities', '/capabilities.html'],
  ['faq', '/faq.html'],
  ['research', '/research/'],
  ['research-validation', '/research/validation.html'],
  ['workflows', '/workflows/'],
  ['safety', '/safety-and-limitations.html'],
  ['not-found', '/404.html']
];
const screenshotRoutes = new Set(['home', 'documentation', 'download', 'research', 'not-found']);

for (const [name, route] of routes) {
  test(`${name} renders, remains crawl-safe, accessible, and responsive`, async ({ page }, testInfo) => {
    const consoleErrors = [];
    const pageErrors = [];
    page.on('console', message => {
      if (message.type() === 'error') consoleErrors.push(message.text());
    });
    page.on('pageerror', error => pageErrors.push(error.message));

    const response = await page.goto(route, { waitUntil: 'domcontentloaded' });
    expect(response, `No document response for ${route}`).not.toBeNull();
    expect(response.status(), `${route} returned ${response.status()}`).toBeLessThan(400);

    const state = await page.evaluate(() => ({
      lang: document.documentElement.lang,
      h1: document.querySelectorAll('h1').length,
      main: document.querySelectorAll('main').length,
      overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      invalidLinks: document.querySelectorAll('a[href=""], a:not([href]), a[href^="javascript:"]').length,
      robots: document.querySelector('meta[name="robots"]')?.getAttribute('content') || '',
      canonical: document.querySelectorAll('link[rel="canonical"]').length,
      ogImage: document.querySelectorAll('meta[property="og:image"]').length,
      twitterCard: document.querySelector('meta[name="twitter:card"]')?.getAttribute('content') || ''
    }));

    expect(state.lang).toBe('en');
    expect(state.h1).toBe(1);
    expect(state.main).toBe(1);
    expect(state.overflow, `${route} overflows horizontally`).toBeLessThanOrEqual(2);
    expect(state.invalidLinks, `${route} contains unusable links`).toBe(0);
    if (route === '/404.html') {
      expect(state.robots.toLowerCase()).toContain('noindex');
    } else {
      expect(state.robots.toLowerCase()).not.toContain('noindex');
      expect(state.canonical).toBe(1);
      expect(state.ogImage).toBe(1);
      expect(state.twitterCard).toBe('summary_large_image');
    }

    const accessibility = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
      .analyze();
    expect(accessibility.violations, JSON.stringify(accessibility.violations, null, 2)).toEqual([]);
    expect(pageErrors, `Page errors on ${route}`).toEqual([]);
    expect(consoleErrors, `Console errors on ${route}`).toEqual([]);

    if (screenshotRoutes.has(name)) {
      await testInfo.attach(`${name}-${testInfo.project.name}`, {
        body: await page.screenshot({ fullPage: true }),
        contentType: 'image/png'
      });
    }
  });
}
