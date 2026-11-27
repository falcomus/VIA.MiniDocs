import { expect, test, type Page } from '@playwright/test';

export const origin = process.env.MINIDOCS_E2E_ORIGIN!;

test('Einladungslink zeigt bei bestehender Anmeldung einen Hinweis und erhält die Sitzung', async ({
  page,
  context,
}) => {
  await page.goto(origin);
  await login(page, 'browser-admin', process.env.MINIDOCS_E2E_ADMIN_PASSWORD!);
  await expect(page.getByRole('heading', { name: 'Administration' })).toBeVisible();
  const recipient = await context.newPage();
  await recipient.goto(origin + '/#/register?invite=' + 'x'.repeat(43));
  await expect(recipient).toHaveURL(origin + '/');
  await expect(recipient.getByText(/Du bist bereits angemeldet/)).toBeVisible();
  await expect(recipient.getByRole('heading', { name: 'Administration' })).toBeVisible();
  await expect(recipient.getByRole('heading', { name: 'Mit Einladung registrieren' })).toHaveCount(
    0,
  );
});

test('Registrierung per kopiertem Link entfernt das Fragment und verweigert Wiederverwendung', async ({
  page,
  context,
}) => {
  const password = process.env.MINIDOCS_E2E_ADMIN_PASSWORD!;
  await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin });
  await page.goto(origin);
  await login(page, 'browser-admin', password);
  await page.getByRole('button', { name: 'Einladung erstellen' }).click();
  const token = await page.locator('output').innerText();
  await page.getByRole('button', { name: 'Einladungslink kopieren' }).click();
  await expect(page.getByText('Einladungslink kopiert.')).toBeVisible();
  const link = await page.evaluate(() => navigator.clipboard.readText());
  expect(link === origin + '/#/register?invite=' + token).toBe(true);
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page.getByRole('heading', { name: 'Anmelden' })).toBeVisible();

  async function openInvite(url: string) {
    await page.goto(url);
    await expect(page).toHaveURL(origin + '/');
    await expect(page.getByRole('heading', { name: 'Mit Einladung registrieren' })).toBeVisible();
  }
  async function register(userName: string) {
    await page.getByLabel('Benutzername').fill(userName);
    await page.getByLabel('Passwort').fill(password);
    await page.getByRole('button', { name: 'Registrieren', exact: true }).click();
  }

  await openInvite(link);
  await expect(page.getByLabel('Einladungstoken')).toHaveCount(0);
  expect(await page.evaluate((value) => JSON.stringify(localStorage).includes(value), token)).toBe(
    false,
  );
  await register('browser-link-member');
  await expect(page.getByRole('status')).toContainText('Registrierung abgeschlossen');
  await login(page, 'browser-link-member', password);
  await expect(page.getByText(/Workspace wird eingerichtet/)).toBeVisible();
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page.getByRole('heading', { name: 'Anmelden' })).toBeVisible();

  await openInvite(link);
  await register('browser-link-replay');
  await expect(page.getByRole('alert')).toHaveText('Die Einladung ist nicht mehr gültig.');
  await expect(page.getByLabel('Einladungstoken')).toBeVisible();
  await openInvite(origin + '/#/register?invite=ungueltig');
  await expect(page.getByRole('alert')).toContainText('Einladungslink ist ungültig');
  await expect(page.getByLabel('Einladungstoken')).toBeVisible();
});

async function login(page: Page, user: string, password: string) {
  await page.getByLabel('Benutzername').fill(user);
  await page.getByLabel('Passwort').fill(password);
  await page.getByRole('button', { name: 'Anmelden', exact: true }).click();
}

test('HTTPS-Sitzung, Einladung, Mandantenrolle und Provisionierung', async ({ page }) => {
  const password = process.env.MINIDOCS_E2E_ADMIN_PASSWORD!;
  await page.goto(origin);
  await login(page, 'browser-admin', 'absichtlich-falsch');
  await expect(page.getByRole('alert')).toContainText('ungültig');
  await login(page, 'browser-admin', password);
  await expect(page.getByRole('heading', { name: 'Administration' })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Administration' })).toBeVisible();
  await page.getByRole('button', { name: 'Einladung erstellen' }).click();
  const token = page.locator('output');
  await expect(token).toHaveText(/^[\w-]{43}$/);
  const invitation = await token.innerText();
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await page.getByRole('button', { name: 'Mit Einladung registrieren' }).click();
  await page.getByLabel('Einladungstoken').fill(invitation);
  await page.getByLabel('Benutzername').fill('browser-member');
  await page.getByLabel('Passwort').fill(password);
  await page.getByRole('button', { name: 'Registrieren', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Registrierung abgeschlossen');
  await login(page, 'browser-member', password);
  await expect(page.getByText(/Workspace wird eingerichtet/)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Administration' })).toHaveCount(0);
  expect((await page.request.get(origin + '/api/v1/admin/users')).status()).toBe(403);
  const session = await (await page.request.get(origin + '/api/v1/session')).json();
  expect(session.workspaceId).toMatch(/^[\da-f-]{36}$/);
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await login(page, 'browser-admin', password);
  await page.getByLabel('Workspace-ID').fill(session.workspaceId);
  await page.getByLabel('Technische Einladung', { exact: true }).fill('c'.repeat(64));
  await page.getByRole('checkbox', { name: /Ich bestätige die Übergabe/ }).check();
  await page.getByRole('button', { name: 'Einladung hinterlegen' }).click();
  await expect(page.getByText(/Verarbeitung wurde angenommen/)).toBeVisible();
  await expect
    .poll(
      async () => {
        const response = await page.request.get(
          origin + '/api/v1/admin/provisioning?workspaceId=' + session.workspaceId,
        );
        expect(response.ok()).toBe(true);
        return (await response.json()).items[0].state;
      },
      { timeout: 20_000 },
    )
    .toBe('active');
  await page.getByRole('button', { name: 'Abmelden' }).click();
  await expect(page.getByRole('heading', { name: 'Anmelden' })).toBeVisible();
  expect((await page.request.get(origin + '/api/v1/session')).status()).toBe(401);
});
