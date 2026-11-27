import { expect, test, type Page, type TestInfo } from '@playwright/test';

async function compare(page: Page, info: TestInfo, name: string) {
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
  if (process.env.MINIDOCS_E2E_CANDIDATES === '1') {
    await page.screenshot({ path: info.outputPath(name), animations: 'disabled' });
  } else {
    await expect(page).toHaveScreenshot(name, { animations: 'disabled' });
  }
}

for (const theme of ['light', 'dark'] as const) {
  test(`Login-Referenz ${theme}`, async ({ page }, info) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await page.goto(process.env.MINIDOCS_E2E_ORIGIN!);
    await expect(page.getByRole('heading', { name: 'Anmelden' })).toBeVisible();
    await page.getByLabel('Darstellung').selectOption(theme);
    const root = page.locator('html');
    await expect(root).toHaveAttribute('data-theme', theme);
    await expect(root).toHaveCSS('color-scheme', theme);
    await compare(page, info, `login-${theme}.png`);
  });
}

test('Referenz der Dokumenten-Demo', async ({ page }, info) => {
  await page.clock.setFixedTime(new Date('2026-11-27T12:00:00+01:00'));
  await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
  await page.goto('https://127.0.0.1:4179');
  const grid = page.getByRole('list', { name: 'Kachelansicht' });
  await expect(grid).toBeVisible();
  const cards = grid.locator('[data-document-id]');
  await expect(cards.first()).toBeVisible();
  for (const card of await cards.all()) {
    await card.scrollIntoViewIfNeeded();
    const preview = card.getByRole('img', { name: 'PDF-Vorschau, erste Seite' });
    await expect(preview).toBeVisible({ timeout: 15_000 });
    await preview.evaluate((image: HTMLImageElement) => image.decode());
  }
  await grid.scrollIntoViewIfNeeded();
  await compare(page, info, 'dokumente.png');
});
