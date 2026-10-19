import { expect, Page, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';

/** CNJ válido (TJSP), com o dígito verificador conferido. */
const CNJ = '2238058-98.2025.8.26.8434';

/** Escolhe a opção de um mat-select pelo rótulo do campo. */
async function choose(page: Page, label: string, option: string): Promise<void> {
  await page.getByLabel(label, { exact: true }).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

/** Avança o assistente pelo botão do passo aberto (os dos outros passos ficam escondidos). */
async function next(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Próximo' }).filter({ visible: true }).last().click();
}

/**
 * Processos pela tela (Etapa 5, PR 5E1): cadastro pelo assistente (CNJ consultado, partes criadas na hora,
 * responsável), suspensão com a linha do tempo, filtro salvo no servidor e operação em lote. Precisa do AppHost.
 */
test.describe('Cases flow', () => {
  test.skip(
    !mailpitUrl,
    'Precisa do AppHost: defina E2E_MAILPIT_URL (ex.: http://localhost:8025).',
  );
  test.setTimeout(240_000);

  test('a case is registered by the wizard, suspended, filtered with a saved filter and reactivated in batch', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, `Escritório Processos ${Date.now()}`);

    // Assistente: tipo e número (com a consulta do CNJ).
    await page.getByRole('link', { name: 'Processos', exact: true }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Processos' })).toBeVisible();
    await page.getByRole('link', { name: 'Novo processo' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Novo processo' })).toBeVisible();
    await page.getByLabel('Número CNJ').fill(CNJ.replace(/\D/g, ''));
    await page.getByRole('button', { name: 'Consultar CNJ' }).click();
    await expect(page.getByRole('status')).toContainText('Ano 2025');
    await expect(page.getByLabel('Número CNJ')).toHaveValue(CNJ);
    await expectAccessible(page);
    await next(page);

    // Dados.
    await page.getByLabel('Comarca').fill('São Paulo');
    await next(page);

    // Partes: cliente e parte contrária cadastrados na hora.
    await page.getByLabel('Nome ou razão social').fill('Banco Alfa S.A.');
    await page.getByRole('button', { name: 'Cadastrar como cliente' }).click();
    await expect(page.getByRole('cell', { name: 'Banco Alfa S.A.' })).toBeVisible();
    await page.getByLabel('Nome ou razão social').fill('Construtora Rival Ltda.');
    await page.getByRole('button', { name: 'Cadastrar como parte contrária' }).click();
    await expect(page.getByRole('cell', { name: 'Construtora Rival Ltda.' })).toBeVisible();
    await next(page);

    // Responsáveis: o próprio administrador.
    await page.getByLabel('Membro', { exact: true }).click();
    await page.getByRole('option').first().click();
    await page.getByRole('button', { name: 'Incluir', exact: true }).first().click();
    await next(page);

    // Revisão e cadastro.
    await page.getByRole('button', { name: 'Cadastrar processo' }).click();
    const heading = page.getByRole('heading', { level: 1 });
    await expect(heading).not.toHaveText('Novo processo');
    const folder = (await heading.textContent())!.trim();
    await expect(page.getByText(CNJ)).toBeVisible();
    await expectAccessible(page);

    // Suspensão e a linha do tempo.
    await page.getByRole('button', { name: 'Suspender' }).click();
    await expect(page.getByText('Suspenso', { exact: true })).toBeVisible();
    await page.getByRole('tab', { name: 'Linha do tempo' }).click();
    await expect(page.getByText('Situação: Ativo → Suspenso')).toBeVisible();
    await expect(page.getByText(`Processo cadastrado (pasta ${folder})`)).toBeVisible();
    await page.getByRole('tab', { name: 'Partes' }).click();
    await expect(page.getByRole('link', { name: 'Construtora Rival Ltda.' })).toBeVisible();
    await expectAccessible(page);

    // Lista: filtro por situação, salvo no servidor e reaplicado depois de recarregar.
    await page.getByRole('link', { name: 'Voltar para os processos' }).click();
    await choose(page, 'Situação', 'Suspenso');
    await expect(page.getByRole('link', { name: folder })).toBeVisible();
    await page.getByLabel('Nome do filtro').fill('Suspensos');
    await page.getByRole('button', { name: 'Salvar filtro' }).click();
    await expect(page.getByRole('button', { name: 'Remover o filtro Suspensos' })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: 'Remover o filtro Suspensos' })).toBeVisible();
    await choose(page, 'Filtros salvos', 'Suspensos');
    await expect(page.getByRole('link', { name: folder })).toBeVisible();
    await expectAccessible(page);

    // Lote: reativar o selecionado.
    await page.getByRole('checkbox', { name: `Selecionar ${folder}` }).check();
    await choose(page, 'Ação', 'Reativar');
    await page.getByRole('button', { name: 'Aplicar aos selecionados' }).click();
    await expect(page.getByRole('status')).toContainText('1 processo(s) atualizado(s).');
    await expect(page.getByRole('link', { name: folder })).toHaveCount(0);
    await expectAccessible(page);
  });
});
