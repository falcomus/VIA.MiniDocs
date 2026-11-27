import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.pw.ts',
  workers: 1,
  retries: 0,
  reporter: 'list',
  outputDir:
    process.env.MINIDOCS_E2E_CANDIDATES === '1' ? 'test-results/candidates' : 'test-results/gate',
  updateSnapshots: 'none',
  snapshotPathTemplate: '{testDir}/reference/{platform}/{arg}{ext}',
  use: {
    browserName: 'chromium',
    viewport: { width: 1440, height: 1000 },
    deviceScaleFactor: 1,
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin',
    // Trust is scoped to the browser test context and its disposable loopback HTTPS host.
    ignoreHTTPSErrors: true,
  },
  projects: [
    { name: 'functional', testIgnore: '**/visual.pw.ts' },
    { name: 'visual', testMatch: '**/visual.pw.ts' },
  ],
  webServer: {
    command: 'node e2e/demo-server.mjs',
    url: 'https://127.0.0.1:4179',
    ignoreHTTPSErrors: true,
    reuseExistingServer: false,
    env: { MINIDOCS_DEV_CERT: '', MINIDOCS_DEV_KEY: '', MINIDOCS_DEV_API: '' },
  },
});
