import { expect, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';

test.describe('Case import', () => {
  test.use({ actionTimeout: 25_000 });
  test.skip(!mailpitUrl, 'Precisa do AppHost, do Worker e E2E_MAILPIT_URL.');
  test.setTimeout(240_000);
  test('validates a mixed CSV, imports only valid rows and downloads the invalid rows report', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, `Escritório Importação ${Date.now()}`);
    await page.getByRole('link', { name: 'Processos', exact: true }).click();
    await page.getByRole('link', { name: 'Importar processos', exact: true }).click();
    await expectAccessible(page);
    const templatePromise = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Baixar modelo' }).click();
    const template = await templatePromise;
    expect(template.suggestedFilename()).toBe('modelo-processos.xlsx');
    expect(await template.failure()).toBeNull();
    const csv =
      'tipo;cliente_nome;pasta;cnj\nextrajudicial;Cliente Importado;LEGADO-001;\njudicial;Cliente Inválido;LEGADO-002;123\nextrajudicial;Outro Cliente;LEGADO-003;\n';
    await page.getByLabel('Arquivo XLSX ou CSV').setInputFiles({
      name: 'processos.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csv, 'utf8'),
    });
    await page.getByRole('button', { name: 'Enviar para validação' }).click();
    await expect(page.getByRole('status')).toHaveText(
      'Validação concluída. Confira antes de importar.',
      { timeout: 90_000 },
    );
    await expect(
      page.getByRole('table', { name: 'Pré-visualização das primeiras linhas válidas' }),
    ).toContainText('Cliente Importado');
    await expect(page.getByRole('table', { name: 'Primeiros erros de validação' })).toContainText(
      '123',
    );
    await expectAccessible(page);
    await page.reload();
    await expect(page.getByRole('status')).toHaveText(
      'Validação concluída. Confira antes de importar.',
    );
    await page.getByRole('button', { name: 'Confirmar importação das válidas' }).click();
    await expect(page.getByRole('status')).toHaveText('Importação concluída.', { timeout: 90_000 });
    await expect(
      page.locator('dt', { hasText: 'Processos importados' }).locator('+ dd'),
    ).toHaveText('2');
    await expect(page.locator('dt', { hasText: 'Linhas inválidas' }).locator('+ dd')).toHaveText(
      '1',
    );
    await expectAccessible(page);
    const reportPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Baixar relatório das inválidas' }).click();
    const report = await reportPromise;
    expect(report.suggestedFilename()).toBe('erros-importacao.xlsx');
    expect(await report.failure()).toBeNull();
    // XLSX é um arquivo ZIP binário. A assinatura evita regressão do responseType para JSON.
    const stream = await report.createReadStream();
    const chunks: Buffer[] = [];
    for await (const chunk of stream!) chunks.push(Buffer.from(chunk));
    expect(Buffer.concat(chunks).subarray(0, 2).toString()).toBe('PK');
    await page.getByRole('link', { name: 'Voltar para os processos' }).click();
    await expect(page.getByRole('link', { name: 'LEGADO-001', exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'LEGADO-003', exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'LEGADO-002', exact: true })).toHaveCount(0);
  });
});
