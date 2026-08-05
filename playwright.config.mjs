import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/site',
  timeout: 30_000,
  expect: { timeout: 5_000 },
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  workers: process.env.CI ? 6 : undefined,
  reporter: [
    ['line'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }]
  ],
  use: {
    browserName: 'chromium',
    channel: 'chrome',
    baseURL: 'http://127.0.0.1:4173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  webServer: {
    command: 'python scripts/prepare-public-site.py && python -m http.server 4173 --directory docs',
    url: 'http://127.0.0.1:4173/',
    reuseExistingServer: !process.env.CI,
    timeout: 30_000
  },
  projects: [
    { name: 'desktop-chrome', use: { viewport: { width: 1280, height: 800 } } },
    { name: 'tablet-chrome', use: { viewport: { width: 768, height: 1024 } } },
    { name: 'mobile-chrome', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } }
  ]
});
