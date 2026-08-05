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

for (const [name, route] of routes) {
  test(`${name} renders, remains crawl-safe, accessible, and responsive`, async ({ page }, testInfo) => {
    const consoleErrors = [];
    const pageErrors = [];
    page.on('console', message => {
      if (message.type() === 'error') consoleErrors.push(message.text());
    });
    page.on('pageerror', error => pageErrors.push(error.message));

    const response = await page.goto(route, { waitUntil: 'networkidle' });
    expect(response, `No document response for ${route}`).not.toBeNull();
    expect(response.status(), `${route} returned ${response.status()}`).toBeLessThan(400);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.locator('main')).toHaveCount(1);

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, `${route} overflows horizontally`).toBeLessThanOrEqual(2);

    const invalidLinks = await page.locator('a[href=""], a:not([href]), a[href^="javascript:"]').count();
    expect(invalidLinks, `${route} contains unusable links`).toBe(0);

    const robots = (await page.locator('meta[name="robots"]').getAttribute('content')) || '';
    if (route === '/404.html') {
      expect(robots.toLowerCase()).toContain('noindex');
    } else {
      expect(robots.toLowerCase()).not.toContain('noindex');
      await expect(page.locator('link[rel="canonical"]')).toHaveCount(1);
      await expect(page.locator('meta[property="og:image"]')).toHaveCount(1);
      await expect(page.locator('meta[name="twitter:card"]')).toHaveAttribute('content', 'summary_large_image');
    }

    const accessibility = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
      .analyze();
    expect(accessibility.violations, JSON.stringify(accessibility.violations, null, 2)).toEqual([]);

    expect(pageErrors, `Page errors on ${route}`).toEqual([]);
    expect(consoleErrors, `Console errors on ${route}`).toEqual([]);

    await testInfo.attach(`${name}-${testInfo.project.name}`, {
      body: await page.screenshot({ fullPage: true }),
      contentType: 'image/png'
    });
  });
}
