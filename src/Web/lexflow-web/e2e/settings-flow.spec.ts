import { expect, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';

/**
 * Telas de configurações do Administrador do escritório (Etapa 3, PR 3F2): renomear o escritório, criar um perfil e
 * mudar a matriz, e cadastrar a estrutura. Precisa do AppHost (cadastro com confirmação pelo Mailpit).
 */
test.describe('Settings flow', () => {
  test.skip(
    !mailpitUrl,
    'Precisa do AppHost: defina E2E_MAILPIT_URL (ex.: http://localhost:8025).',
  );
  test.setTimeout(180_000);

  test('the office admin renames the office, edits a role and builds the structure', async ({
    page,
    request,
  }) => {
    const office = `Escritório Config ${Date.now()}`;
    await createOfficeAsNewAdmin(page, request, office);
    await page.getByRole('link', { name: 'Configurações' }).click();

    // Escritório: novo nome aparece no topo.
    await page.getByRole('tab', { name: 'Escritório' }).click();
    await expect(page.getByLabel('Nome do escritório')).toHaveValue(office);
    await expectAccessible(page);
    await page.getByLabel('Nome do escritório').fill(`${office} Renomeado`);
    await page.getByRole('button', { name: 'Salvar' }).click();
    await expect(page.getByRole('status')).toContainText('Alterações salvas.');
    await expect(page.getByTestId('active-organization')).toHaveText(`${office} Renomeado`);

    // Perfis: cria um perfil e liga uma permissão na matriz.
    await page.getByRole('tab', { name: 'Perfis' }).click();
    await page.getByLabel('Nome do novo perfil').fill('Estagiário de férias');
    await page.getByRole('button', { name: 'Criar perfil' }).click();
    await expect(page.getByLabel('Nome', { exact: true })).toHaveValue('Estagiário de férias');
    await page.getByRole('checkbox', { name: 'Ver processos e casos' }).check();
    await expectAccessible(page);
    await page.getByRole('button', { name: 'Salvar permissões' }).click();
    await expect(page.getByRole('status')).toContainText('Alterações salvas.');
    await expect(page.getByRole('checkbox', { name: 'Ver processos e casos' })).toBeChecked();

    // Estrutura: área e equipe ligada a ela; a área em uso não pode ser excluída.
    await page.getByRole('tab', { name: 'Estrutura' }).click();
    await page.getByRole('tab', { name: 'Áreas' }).click();
    const areas = page.getByRole('tabpanel', { name: 'Áreas' });
    await areas.getByLabel('Nome').fill('Trabalhista');
    await areas.getByRole('button', { name: 'Adicionar' }).click();
    await expect(areas.getByRole('cell', { name: 'Trabalhista' })).toBeVisible();

    await page.getByRole('tab', { name: 'Equipes' }).click();
    const teams = page.getByRole('tabpanel', { name: 'Equipes' });
    await teams.getByLabel('Nome').fill('Contencioso trabalhista');
    await teams.getByRole('combobox', { name: 'Área' }).click();
    await page.getByRole('option', { name: 'Trabalhista' }).click();
    await teams.getByRole('button', { name: 'Adicionar' }).click();
    await expect(teams.getByRole('cell', { name: 'Contencioso trabalhista' })).toBeVisible();
    await expectAccessible(page);

    await page.getByRole('tab', { name: 'Áreas' }).click();
    await areas.getByRole('button', { name: 'Remover' }).click();
    await expect(page.getByRole('alert')).toContainText('em uso');
  });
});
