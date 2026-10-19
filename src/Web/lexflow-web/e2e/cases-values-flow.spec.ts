import { expect, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';
import { choose, createSimpleCase, openCaseTab } from './support/cases';

test.describe('Case values and follow-up', () => {
  test.use({ actionTimeout: 25_000 });
  test.skip(!mailpitUrl, 'Precisa do AppHost e E2E_MAILPIT_URL.');
  test.setTimeout(300_000);
  test('claims, risk, settlement installments and a rescheduled hearing are managed by the screens', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, `Escritório Valores ${Date.now()}`);
    await createSimpleCase(page);
    await openCaseTab(page, 'Pedidos e valores');
    await page.getByRole('button', { name: 'Novo pedido', exact: true }).click();
    await choose(page, 'Objeto do pedido', 'Danos morais');
    await page.getByLabel('Descrição', { exact: true }).fill('Reparação do dano');
    await choose(page, 'Índice de correção', 'INPC');
    await page.getByLabel('Início da correção', { exact: true }).fill('2020-01-15');
    const first = page.getByRole('group', { name: '1ª instância', exact: true });
    await first.getByLabel('Pedido', { exact: true }).fill('10.000,00');
    await first.getByLabel('Estimado', { exact: true }).fill('8.000,00');
    await page.getByRole('button', { name: 'Salvar pedido', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Danos morais', exact: true })).toBeVisible();
    await page.getByLabel('Atualizar até').fill('2021-01-15');
    await page.getByRole('button', { name: 'Atualizar valores' }).click();
    const valuations = page.getByRole('table', { name: 'Valores por instância', exact: true });
    await expect(valuations).toContainText('10.544,73');
    await expectAccessible(page);
    await page.getByRole('button', { name: 'Editar pedido', exact: true }).click();
    await first.getByLabel('Pedido', { exact: true }).fill('12.000,00');
    await page.getByRole('button', { name: 'Salvar pedido', exact: true }).click();
    await page.getByRole('button', { name: 'Histórico de valores', exact: true }).click();
    const history = page.getByRole('table', { name: 'Histórico de valores', exact: true });
    await expect(history).toContainText('10.000,00');
    await expect(history).toContainText('12.000,00');
    await expectAccessible(page);

    await openCaseTab(page, 'Risco');
    await choose(page, 'Âmbito da avaliação', 'Reparação do dano');
    await choose(page, 'Probabilidade de perda', 'Provável');
    await page.getByLabel('Perda estimada').fill('8.000,00');
    await page.getByLabel('Justificativa').fill('Provas documentais');
    await page.getByRole('button', { name: 'Registrar avaliação' }).click();
    await expect(page.getByRole('table', { name: 'Avaliações atuais' })).toContainText('8.000,00');
    await expect(page.getByRole('table', { name: 'Histórico de risco' })).toContainText(
      'Provas documentais',
    );
    await expectAccessible(page);

    await openCaseTab(page, 'Acordos');
    await page.getByRole('button', { name: 'Novo acordo', exact: true }).click();
    await page.getByRole('button', { name: 'Abrir negociação' }).click();
    await expect(page.getByRole('heading', { name: 'Cliente paga · Em negociação' })).toBeVisible();
    await page.getByRole('button', { name: 'Nova rodada', exact: true }).click();
    await page.getByLabel('Valor da proposta').fill('100,00');
    await page.getByLabel('Condições', { exact: true }).fill('Pagamento parcelado');
    await page.getByRole('button', { name: 'Registrar proposta' }).click();
    await expect(page.getByRole('table', { name: 'Rodadas da negociação' })).toContainText(
      'Pagamento parcelado',
    );
    await page.getByRole('button', { name: 'Enviar termos', exact: true }).click();
    await page.getByLabel('Valor acordado', { exact: true }).fill('100,00');
    await page.getByLabel('Número de parcelas').fill('3');
    await page.getByLabel('Primeiro vencimento').fill('2026-01-31');
    const preview = page.getByRole('table', { name: 'Prévia das parcelas' });
    await expect(preview).toContainText('33,34');
    await expect(preview).toContainText('28/02/2026');
    await page.getByRole('button', { name: 'Confirmar envio dos termos' }).click();
    await expect(
      page.getByRole('heading', { name: 'Cliente paga · Aguardando aprovação' }),
    ).toBeVisible();
    await expectAccessible(page);
    await page.getByRole('button', { name: 'Aprovar acordo', exact: true }).click();
    await page.getByRole('button', { name: 'Confirmar aprovação', exact: true }).click();
    await page.getByRole('button', { name: 'Homologar acordo', exact: true }).click();
    await page.getByLabel('Data da homologação').fill('2026-01-30');
    await page.getByRole('button', { name: 'Confirmar ação' }).click();
    await expect(page.getByRole('heading', { name: 'Cliente paga · Homologado' })).toBeVisible();
    await expect(page.getByRole('table', { name: 'Parcelas do acordo' })).toContainText('Vencida');
    await page.getByRole('button', { name: 'Dar baixa na parcela 1', exact: true }).click();
    await page.getByLabel('Data do pagamento').fill('2026-01-31');
    await page.getByRole('button', { name: 'Confirmar baixa' }).click();
    const installments = page.getByRole('table', { name: 'Parcelas do acordo' });
    await expect(installments.getByRole('row').nth(1)).toContainText('Paga');
    await expectAccessible(page);

    await openCaseTab(page, 'Audiências');
    await page.getByRole('button', { name: 'Agendar audiência', exact: true }).click();
    await choose(page, 'Formato', 'Híbrida');
    await page.getByLabel('Data e hora da audiência').fill('2026-11-20T09:00');
    await page.getByLabel('Local', { exact: true }).fill('Fórum central');
    await page.getByLabel('Link da audiência').fill('https://example.com/audiencia');
    await choose(page, 'Advogado designado', 'Ana Administradora');
    await page.getByLabel('Buscar preposto ou testemunha').fill('Cliente Valores');
    await page.getByRole('button', { name: 'Usar como preposto' }).click();
    await page.getByRole('button', { name: 'Incluir testemunha' }).click();
    await page.getByRole('button', { name: 'Salvar audiência', exact: true }).click();
    await expect(
      page.getByRole('heading', { name: 'Conciliação · 20/11/2026, 09:00' }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Redesignar audiência', exact: true }).click();
    await page.getByLabel('Nova data e hora').fill('2026-11-25T10:30');
    await page.getByLabel('Motivo', { exact: true }).fill('Determinação do juízo');
    await page.getByRole('button', { name: 'Confirmar audiência' }).click();
    await expect(page.getByRole('table', { name: 'Histórico de redesignações' })).toContainText(
      'Determinação do juízo',
    );
    await expectAccessible(page);
    await page.getByRole('button', { name: 'Registrar realização', exact: true }).click();
    await page.getByLabel('Resultado ou ata', { exact: true }).fill('Ata juntada aos autos');
    await page.getByRole('button', { name: 'Confirmar audiência' }).click();
    await expect(page.getByText('Realizada · Híbrida')).toBeVisible();
    await expect(page.getByText('Ata juntada aos autos', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Editar audiência', exact: true })).toHaveCount(
      0,
    );
    await expectAccessible(page);
  });
});
