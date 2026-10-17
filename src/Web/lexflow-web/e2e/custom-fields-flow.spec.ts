import { expect, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';

/**
 * Critério de aceite da Etapa 3: campo customizado obrigatório criado na tela aparece e é validado no formulário de
 * teste (no navegador e no servidor). Precisa do AppHost (cadastro com confirmação pelo Mailpit).
 */
test.describe('Custom fields flow', () => {
  test.skip(
    !mailpitUrl,
    'Precisa do AppHost: defina E2E_MAILPIT_URL (ex.: http://localhost:8025).',
  );
  test.setTimeout(180_000);

  test('a required field created on the screen appears and is validated in the test form', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, `Escritório Campos ${Date.now()}`);

    await page.getByRole('link', { name: 'Configurações' }).click();
    await page.getByRole('tab', { name: 'Campos customizados' }).click();
    await expect(
      page.getByRole('heading', { level: 2, name: 'Campos customizados' }),
    ).toBeVisible();
    await expect(page.getByText('Nenhum campo para esta entidade.')).toBeVisible();
    await expectAccessible(page);

    // Campo de texto obrigatório em "Processos e casos".
    await page.getByLabel('Rótulo').fill('Número da pasta');
    await page.getByRole('checkbox', { name: 'Obrigatório', exact: true }).check();
    await page.getByRole('button', { name: 'Criar campo' }).click();
    await expect(page.getByRole('cell', { name: 'numero_da_pasta' })).toBeVisible();

    // O campo aparece no formulário de teste e a validação recusa o valor vazio.
    const testForm = page.locator('lf-custom-fields');
    const folder = testForm.getByLabel('Número da pasta');
    await expect(folder).toBeVisible();
    await page.getByRole('button', { name: 'Validar' }).click();
    await expect(testForm.getByText('Preencha este campo.')).toBeVisible();
    await expect(page.getByRole('alert')).toContainText('Corrija os campos destacados.');

    // Preenchido, o servidor aceita.
    await folder.fill('P-2026-001');
    await page.getByRole('button', { name: 'Validar' }).click();
    await expect(page.getByRole('status')).toContainText('Valores válidos.');
    await expectAccessible(page);
  });
});
