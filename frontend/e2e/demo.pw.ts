import { expect, test } from '@playwright/test';

test('Demo erstellt und benennt ein Thema um, verschiebt und öffnet ein Dokument', async ({
  page,
}) => {
  await page.goto('https://127.0.0.1:4179');
  const topics = page.getByRole('navigation', { name: 'Themen' });
  await page.getByRole('button', { name: 'Neues Thema auf oberster Ebene' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Name', { exact: true }).fill('Browserprüfung');
  await dialog.locator('button[type=submit]').click();
  await topics.getByText('Browserprüfung', { exact: true }).click({ button: 'right' });
  await page.getByText('Umbenennen', { exact: true }).click();
  await dialog.getByLabel('Name', { exact: true }).fill('Browserziel');
  await dialog.locator('button[type=submit]').click();
  await topics.getByRole('button', { name: 'Finanzen: Unterthemen aufklappen' }).click();
  await topics.getByRole('button', { name: 'Rechnungen', exact: true }).click();
  await page.getByRole('button', { name: 'Listenansicht' }).click();
  const document = page.locator('tr[draggable=true]').first();
  await expect(document).toBeVisible();
  const title = (await document.getByRole('checkbox').getAttribute('aria-label'))!.replace(
    / auswählen$/,
    '',
  );
  await document.dragTo(topics.getByText('Browserziel', { exact: true }));
  await topics.getByText('Browserziel', { exact: true }).click();
  const moved = page.getByRole('row').filter({
    has: page.getByRole('checkbox', { name: title + ' auswählen', exact: true }),
  });
  await expect(moved).toBeVisible();
  await moved.press('Enter');
  await expect(page.getByRole('dialog').getByRole('button', { name: 'Schließen' })).toBeVisible();
});

test('Demo schränkt Ergebnisse über Suche und Datumsfilter ein', async ({ page }) => {
  await page.goto('https://127.0.0.1:4179');
  await page.getByRole('button', { name: 'Listenansicht' }).click();
  await page.getByRole('button', { name: 'Nur Titel' }).click();
  const documents = page.locator('tr[draggable=true]');
  await expect(documents.first()).toBeVisible();
  const title = (await documents.first().getByRole('checkbox').getAttribute('aria-label'))!.replace(
    / auswählen$/,
    '',
  );
  const before = await documents.count();
  await page.getByRole('searchbox', { name: 'Dokumente suchen' }).fill(title);
  await expect.poll(() => documents.count()).toBeLessThan(before);
  await expect(documents.first()).toBeVisible();
  await page.getByRole('button', { name: 'Filter', exact: true }).click();
  await page.getByLabel('Bis', { exact: true }).fill('1900-01-01');
  await expect(documents).toHaveCount(0);
  await page.getByRole('button', { name: 'Filter zurücksetzen' }).click();
  await expect(documents.first()).toBeVisible();
});
