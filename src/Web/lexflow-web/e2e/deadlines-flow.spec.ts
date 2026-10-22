import { expect, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';
import { choose, createSimpleCase, openCaseTab } from './support/cases';

test.describe('Deadline screens', () => {
  test.skip(!mailpitUrl, 'Precisa do AppHost com Worker e Mailpit.');
  test.setTimeout(240_000);
  test('manual registration requires current confirmation and review before completion', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, 'Escritório Prazos');
    const [created] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith('/api/v1/cases') && r.request().method() === 'POST',
      ),
      createSimpleCase(page),
    ]);
    const casePath = new URL(page.url()).pathname;
    const caseId = casePath.split('/').at(-1)!;
    const headers = Object.fromEntries(
      Object.entries(await created.request().allHeaders()).filter(([key]) =>
        ['authorization', 'x-organization-id'].includes(key),
      ),
    );
    const origin = new URL(created.url()).origin;
    const policy = await page.request.put(`${origin}/api/v1/organization/deadline-review`, {
      headers,
      data: { requireDeadlineReview: true },
    });
    expect(policy.status()).toBe(200);
    await page.goto(`/prazos/novo?caseId=${caseId}`);
    await page.getByLabel('Tipo de prazo', { exact: true }).fill('Contestação');
    await page.getByLabel('Descrição', { exact: true }).fill('Prazo manual conferido');
    await choose(page, 'Responsável', 'Ana Administradora');
    const day = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(
      new Date(),
    );
    await page.getByLabel('Data do fato ou da publicação', { exact: true }).fill(day);
    await page.getByRole('button', { name: 'Calcular sugestão', exact: true }).click();
    await page.getByRole('button', { name: 'Aplicar datas sugeridas', exact: true }).click();
    await page
      .getByRole('checkbox', { name: 'Conferi e confirmo as datas sugeridas', exact: true })
      .check();
    await page.getByLabel('Dias do prazo', { exact: true }).fill('5');
    await expect(page.getByRole('button', { name: 'Salvar prazo', exact: true })).toBeDisabled();
    await page.getByRole('button', { name: 'Calcular sugestão', exact: true }).click();
    await page.getByRole('button', { name: 'Aplicar datas sugeridas', exact: true }).click();
    await page
      .getByRole('checkbox', { name: 'Conferi e confirmo as datas sugeridas', exact: true })
      .check();
    await expectAccessible(page);
    const [registered] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith(`/cases/${caseId}/deadlines`) && r.request().method() === 'POST',
      ),
      page.getByRole('button', { name: 'Salvar prazo', exact: true }).click(),
    ]);
    expect(registered.status()).toBe(201);
    await expect(page).toHaveURL(/\/prazos\/[\da-f-]+$/);
    await expect(
      page.getByRole('heading', { name: 'Prazo manual conferido', exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText('Conferência obrigatória pendente', { exact: false }),
    ).toBeVisible();
    const [started] = await Promise.all([
      page.waitForResponse((r) => r.url().endsWith('/start') && r.request().method() === 'POST'),
      page.getByRole('button', { name: 'Iniciar', exact: true }).click(),
    ]);
    expect(started.status()).toBe(200);
    await expect(page.getByText(/Situação:\s*Em andamento/)).toBeVisible();
    await page.getByLabel('Evidência do cumprimento', { exact: true }).fill('Protocolo manual 789');
    await expect(page.getByRole('button', { name: 'Cumprir prazo', exact: true })).toBeDisabled();
    // Escritório com um único membro ativo permite a conferência pelo próprio autor.
    await page.getByRole('button', { name: 'Conferir prazo', exact: true }).click();
    await expect(page.getByLabel('Evidência do cumprimento', { exact: true })).toHaveValue(
      'Protocolo manual 789',
    );
    await expect(page.getByRole('button', { name: 'Cumprir prazo', exact: true })).toBeEnabled();
    await expectAccessible(page);
    await page.getByRole('button', { name: 'Cumprir prazo', exact: true }).click();
    await expect(page.getByText('Protocolo manual 789', { exact: false })).toBeVisible();
    await page.goto('/prazos');
    await page.getByRole('checkbox', { name: 'Incluir encerrados', exact: true }).check();
    await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
    await expect(
      page.getByRole('link', { name: 'Prazo manual conferido', exact: true }),
    ).toBeVisible();
    await expectAccessible(page);
    await expect(page).toHaveURL(/includeClosed=true/);
    await page.getByLabel('Nome do filtro', { exact: true }).fill('Prazos encerrados');
    const [savedFilter] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith('/api/v1/me/preferences') && r.request().method() === 'PUT',
      ),
      page.getByRole('button', { name: 'Salvar filtros atuais', exact: true }).click(),
    ]);
    expect(savedFilter.status()).toBe(200);
    await page.reload();
    await expect(
      page.getByRole('checkbox', { name: 'Incluir encerrados', exact: true }),
    ).toBeChecked();
    await expect(
      page.getByRole('button', { name: 'Prazos encerrados', exact: true }),
    ).toBeVisible();
    await page.emulateMedia({ colorScheme: 'dark' });
    await expectAccessible(page);
    await page.emulateMedia({ colorScheme: 'light' });
    await page.goto(casePath);
    await openCaseTab(page, 'Prazos');
    await expect(page.getByRole('cell', { name: 'Cumprido', exact: true })).toBeVisible();
    await expectAccessible(page);
    const lost = await page.request.post(registered.url(), {
      headers,
      data: { ...registered.request().postDataJSON(), title: 'Prazo com perda justificada' },
    });
    expect(lost.status()).toBe(201);
    await page.goto(`/prazos/${(await lost.json()).id}`);
    await expect(
      page.getByRole('button', { name: 'Marcar como perdido', exact: true }),
    ).toBeDisabled();
    await page
      .getByLabel('Justificativa da perda', { exact: true })
      .fill('Falha operacional registrada no teste');
    await page.getByRole('button', { name: 'Marcar como perdido', exact: true }).click();
    await expect(
      page.getByText('Falha operacional registrada no teste', { exact: false }),
    ).toBeVisible();
    await expect(
      page.getByRole('button', { name: 'Marcar como perdido', exact: true }),
    ).toHaveCount(0);
    await expectAccessible(page);
  });
});
