import { expect, test } from '@playwright/test';

test('Workspace behält das gewählte Thema bei wiederholtem Tabwechsel', async ({
  page,
  context,
}) => {
  const origin = process.env.MINIDOCS_E2E_ORIGIN!;
  await page.goto(origin);
  await page.getByLabel('Benutzername').fill('browser-d6');
  await page.getByLabel('Passwort').fill(process.env.MINIDOCS_E2E_ADMIN_PASSWORD!);
  await page.getByRole('button', { name: 'Anmelden', exact: true }).click();
  await page.getByRole('button', { name: 'Neues Thema auf oberster Ebene' }).click();
  await page.getByRole('dialog').getByLabel('Name', { exact: true }).fill('Tabwechsel-Thema');
  await page.getByRole('dialog').locator('button[type=submit]').click();
  const heading = page.getByRole('heading', { name: 'Tabwechsel-Thema', exact: true });
  await expect(heading).toBeVisible();
  const other = await context.newPage();
  try {
    await other.goto('about:blank');
    for (let change = 0; change < 2; change++) {
      await other.bringToFront();
      const hidden = await page.evaluate(() => document.visibilityState === 'hidden');
      const checked = page.waitForResponse(
        (reply) => reply.url() === origin + '/api/v1/session' && reply.request().method() === 'GET',
      );
      await page.bringToFront();
      // Headless Chromium can keep both targets visible despite switching the foreground target.
      if (!hidden) await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
      expect((await checked).status()).toBe(200);
      await expect(heading).toBeVisible();
    }
  } finally {
    await other.close();
  }
});

test('persistente Dokumentübersicht: Drag & Drop, Undo und Reload', async ({ page }) => {
  await page.goto(process.env.MINIDOCS_E2E_ORIGIN!);
  await page.getByLabel('Benutzername').fill('browser-d6');
  await page.getByLabel('Passwort').fill(process.env.MINIDOCS_E2E_ADMIN_PASSWORD!);
  await page.getByRole('button', { name: 'Anmelden', exact: true }).click();
  const topics = page.getByRole('navigation', { name: 'Themen' });
  const overview = page.getByRole('region', { name: 'Dokumentübersicht' });
  const doc = overview
    .locator('[data-document-id]')
    .filter({ hasText: 'Persistentes Testdokument' });
  await expect(doc).toBeVisible();
  const inbox = topics.getByRole('button', { name: 'Nicht zugeordnet', exact: true });
  await page.getByRole('button', { name: 'Neues Thema auf oberster Ebene' }).click();
  await page
    .getByRole('dialog')
    .getByLabel('Name', { exact: true })
    .fill('Persistentes Browserziel');
  await page.getByRole('dialog').locator('button[type=submit]').click();
  const target = topics.getByRole('button', { name: 'Persistentes Browserziel', exact: true });
  await expect(target).toBeVisible();
  await inbox.click();
  await doc.dragTo(target);
  await expect(page.getByRole('heading', { name: 'Persistentes Browserziel' })).toBeVisible();
  await expect(doc).toBeVisible();
  await expect(page.getByText('1 Dokument verschoben', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Rückgängig' }).click();
  await expect(doc).toHaveCount(0);
  await inbox.click();
  await expect(doc).toBeVisible();
  await doc.dragTo(target);
  await expect(page.getByRole('heading', { name: 'Persistentes Browserziel' })).toBeVisible();
  await page.reload();
  await target.click();
  await expect(doc).toBeVisible();
});

test('legt einen Tag aus der persistenten Dokumentauswahl an und erhält die Zuordnung nach Reload', async ({
  page,
}) => {
  await page.goto(process.env.MINIDOCS_E2E_ORIGIN!);
  await page.getByLabel('Benutzername').fill('browser-d6');
  await page.getByLabel('Passwort').fill(process.env.MINIDOCS_E2E_ADMIN_PASSWORD!);
  await page.getByRole('button', { name: 'Anmelden', exact: true }).click();
  await page.getByRole('searchbox', { name: 'Dokumente suchen' }).fill('Persistentes Testdokument');
  const doc = page.locator('[data-document-id]').filter({ hasText: 'Persistentes Testdokument' });
  const dialog = page.getByRole('dialog');
  await doc.getByRole('button', { name: 'Tags zuweisen' }).click();
  await dialog.getByRole('button', { name: '+ Neuer Tag' }).click();
  await dialog.getByLabel('Name', { exact: true }).fill('Browser-Abnahme');
  await dialog.getByRole('button', { name: 'Speichern' }).click();
  await expect(dialog.getByRole('checkbox', { name: 'Browser-Abnahme' })).toBeChecked();
  await dialog.getByRole('button', { name: 'Fertig' }).click();
  await page.reload();
  await page.getByRole('searchbox', { name: 'Dokumente suchen' }).fill('Persistentes Testdokument');
  await doc.getByRole('button', { name: 'Tags zuweisen' }).click();
  await expect(dialog.getByRole('checkbox', { name: 'Browser-Abnahme' })).toBeChecked();
});

test('zwei Tabs: älterer Themenentwurf darf neuere Serverversion nicht überschreiben', async ({
  page,
  context,
}) => {
  await page.goto(process.env.MINIDOCS_E2E_ORIGIN!);
  await page.getByLabel('Benutzername').fill('browser-d6');
  await page.getByLabel('Passwort').fill(process.env.MINIDOCS_E2E_ADMIN_PASSWORD!);
  await page.getByRole('button', { name: 'Anmelden', exact: true }).click();
  await page.getByRole('button', { name: 'Neues Thema auf oberster Ebene' }).click();
  await page.getByRole('dialog').getByLabel('Name', { exact: true }).fill('Konflikt-Thema');
  await page.getByRole('dialog').locator('button[type=submit]').click();
  await expect(
    page
      .getByRole('navigation', { name: 'Themen' })
      .getByRole('button', { name: 'Konflikt-Thema', exact: true }),
  ).toBeVisible();
  const other = await context.newPage();
  try {
    await other.goto(process.env.MINIDOCS_E2E_ORIGIN!);
    for (const tab of [page, other]) {
      const node = tab
        .getByRole('navigation', { name: 'Themen' })
        .getByRole('button', { name: 'Konflikt-Thema', exact: true });
      await node.focus();
      await node.press('Shift+F10');
      await tab.getByRole('menuitem', { name: 'Umbenennen' }).click();
    }
    await page.getByRole('dialog').getByLabel('Name', { exact: true }).fill('Alter Entwurf');
    await other.getByRole('dialog').getByLabel('Name', { exact: true }).fill('Neue Serverversion');
    await other.getByRole('dialog').locator('button[type=submit]').click();
    await expect(other.getByRole('dialog')).toHaveCount(0);
    await expect(
      page.locator('[data-topic-id]').filter({ hasText: 'Neue Serverversion' }),
    ).toHaveCount(1);
    const response = page.waitForResponse(
      (response) =>
        response.request().method() === 'PATCH' && response.url().includes('/api/v1/topics/'),
    );
    await page.getByRole('dialog').locator('button[type=submit]').click();
    expect((await response).status()).toBe(412);
    await expect(page.getByRole('dialog').getByRole('alert')).toContainText(
      'zwischenzeitlich geändert',
    );
    await page.getByRole('dialog').getByRole('button', { name: 'Abbrechen' }).click();
    await page.reload();
    await expect(
      page
        .getByRole('navigation', { name: 'Themen' })
        .getByRole('button', { name: 'Neue Serverversion', exact: true }),
    ).toBeVisible();
  } finally {
    await other.close();
  }
});
