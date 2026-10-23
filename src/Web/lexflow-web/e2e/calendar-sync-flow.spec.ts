import { expect, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';
import { choose } from './support/cases';

test.describe('Calendar synchronization', () => {
  test.skip(!mailpitUrl, 'Precisa do AppHost com Worker e Mailpit.');
  test.setTimeout(240_000);

  test('simulated connections persist, synchronize and retain their disconnected state', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, 'Escritório Calendário 7G');
    await page.goto('/agenda');
    await page.getByRole('link', { name: 'Sincronização de calendários', exact: true }).click();
    await expect(
      page.getByText('Simulação local: nenhum dado é enviado ao Google ou Microsoft', {
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      page.getByText('Nenhuma conexão de calendário cadastrada.', { exact: true }),
    ).toBeVisible();
    await page.getByLabel('Início', { exact: true }).fill('2026-10-01');
    await page.getByLabel('Fim exclusivo', { exact: true }).fill('2026-11-01');
    for (const provider of ['Google Calendar', 'Microsoft 365']) {
      await choose(page, 'Provedor', provider);
      const [saved] = await Promise.all([
        page.waitForResponse(
          (r) => r.url().endsWith('/me/calendar-connections') && r.request().method() === 'POST',
        ),
        page.getByRole('button', { name: 'Salvar conexão e período', exact: true }).click(),
      ]);
      expect(saved.status()).toBe(200);
      const connection = await saved.json();
      await expect(
        page.getByRole('button', { name: new RegExp(`^${provider} ·`) }),
      ).toHaveAttribute('aria-pressed', 'true');
      const [queued] = await Promise.all([
        page.waitForResponse(
          (r) => r.url().endsWith(`/${connection.id}/sync`) && r.request().method() === 'POST',
        ),
        page.getByRole('button', { name: 'Sincronizar agora', exact: true }).click(),
      ]);
      expect(queued.status()).toBe(202);
      await expect(async () => {
        await page.getByRole('button', { name: 'Atualizar', exact: true }).click();
        const row = page
          .locator('li')
          .filter({ has: page.getByRole('button', { name: new RegExp(`^${provider} ·`) }) });
        await expect(row).toContainText('Última sincronização:');
      }).toPass({ timeout: 60_000 });
    }
    await page.reload();
    await expect(page.getByRole('button', { name: /^Google Calendar ·/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Microsoft 365 ·/ })).toBeVisible();
    await expectAccessible(page);
    await page.getByRole('button', { name: /^Google Calendar ·/ }).click();
    await expect(page.getByRole('button', { name: 'Desconectar', exact: true })).toBeDisabled();
    await page.getByRole('checkbox', { name: 'Confirmo a desconexão.', exact: false }).check();
    const [disconnected] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith('/disconnect') && r.request().method() === 'POST',
      ),
      page.getByRole('button', { name: 'Desconectar', exact: true }).click(),
    ]);
    expect(disconnected.status()).toBe(200);
    await expect(page.getByRole('button', { name: /^Google Calendar ·/ })).toContainText(
      'Desconectada',
    );
    await expect(
      page.getByRole('button', { name: 'Sincronizar agora', exact: true }),
    ).toBeDisabled();
    await expectAccessible(page);
  });
});
