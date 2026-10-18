import { expect, Page, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';

/** CNPJ válido usado nos dois escritórios (o critério de aceite exige o mesmo documento em tenants diferentes). */
const CNPJ = '11.222.333/0001-81';

/** Cadastra uma pessoa jurídica pela tela e espera o resultado (detalhe ou erro). */
async function createCompany(
  page: Page,
  name: string,
  role: string,
  document: string | null = null,
): Promise<void> {
  await page.getByRole('link', { name: 'Pessoas', exact: true }).click();
  await page.getByRole('link', { name: 'Nova pessoa' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Nova pessoa' })).toBeVisible();
  if (document) {
    await page.getByLabel('CNPJ').fill(document);
  }
  await page.getByLabel('Razão social').fill(name);
  await page.getByRole('checkbox', { name: role, exact: true }).check();
  await page.getByRole('button', { name: 'Salvar' }).click();
}

/**
 * Critérios de aceite da Etapa 4 pela tela (Etapa 4, PR 4D1): documento duplicado bloqueado no mesmo escritório e
 * permitido em outro; busca sem acentos; conflito direto que só prossegue com justificativa. Precisa do AppHost.
 */
test.describe('People and conflict flow', () => {
  test.skip(
    !mailpitUrl,
    'Precisa do AppHost: defina E2E_MAILPIT_URL (ex.: http://localhost:8025).',
  );
  test.setTimeout(240_000);

  test('a duplicate document is refused in the office, allowed in another, and the search ignores accents', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, `Escritório Pessoas ${Date.now()}`);

    await createCompany(page, 'João da Silva Ltda.', 'Cliente', CNPJ);
    await expect(
      page.getByRole('heading', { level: 1, name: 'João da Silva Ltda.' }),
    ).toBeVisible();
    await expect(page.getByText(CNPJ)).toBeVisible();
    await expectAccessible(page);

    // Busca sem acento encontra o nome mais longo.
    await page.getByRole('link', { name: 'Pessoas', exact: true }).click();
    await page.getByLabel('Buscar por nome ou CPF/CNPJ').fill('Joao da Silva');
    await expect(page.getByRole('link', { name: 'João da Silva Ltda.' })).toBeVisible();
    await expectAccessible(page);

    // O mesmo CNPJ no mesmo escritório: aviso de duplicata e recusa do servidor.
    await createCompany(page, 'Outra Empresa', 'Fornecedor', CNPJ);
    await expect(page.getByRole('alert')).toContainText(
      'Já existe uma pessoa com este CPF/CNPJ no escritório.',
    );
    await expect(page.getByText('mesmo documento')).toBeVisible();
    await expectAccessible(page);

    // Em outro escritório, o mesmo CNPJ é aceito.
    await page.goto('/escritorio/novo');
    await page.getByLabel('Nome do escritório').fill(`Segundo Escritório ${Date.now()}`);
    await page.getByRole('button', { name: 'Criar escritório' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Início' })).toBeVisible();
    await createCompany(page, 'João da Silva Ltda.', 'Cliente', CNPJ);
    await expect(
      page.getByRole('heading', { level: 1, name: 'João da Silva Ltda.' }),
    ).toBeVisible();
  });

  test('a future client who is already an opponent is a direct conflict that needs a justification', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, `Escritório Conflito ${Date.now()}`);
    await createCompany(page, 'Construtora Rival Ltda.', 'Parte contrária');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Construtora Rival Ltda.' }),
    ).toBeVisible();

    await page.getByRole('link', { name: 'Conflito de interesses' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Conflito de interesses' }),
    ).toBeVisible();
    await page.getByRole('radio', { name: 'Será cliente' }).check();
    await page.getByLabel('Nome ou razão social').fill('Construtora Rival Ltda.');
    await page.getByRole('button', { name: 'Verificar' }).click();

    await expect(page.getByRole('heading', { level: 2, name: /Conflito direto/ })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Mesmo nome' })).toBeVisible();
    const proceed = page.getByRole('button', { name: 'Prosseguir' });
    await expect(proceed).toBeDisabled();
    await expectAccessible(page);

    await page.getByLabel('Justificativa').fill('Causa sem relação com o processo anterior.');
    await proceed.click();
    await expect(page.getByRole('status')).toContainText('Verificação concluída');

    await page.getByRole('link', { name: 'Relatório de verificações' }).click();
    await expect(page.getByRole('cell', { name: 'Construtora Rival Ltda.' })).toBeVisible();
    await expect(page.getByText('Causa sem relação com o processo anterior.')).toBeVisible();
    await expectAccessible(page);
  });
});
