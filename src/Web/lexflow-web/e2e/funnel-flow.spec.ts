import { expect, Page, test } from '@playwright/test';
import {
  createOfficeAsNewAdmin,
  expectAccessible,
  mailpitUrl,
  newEmail,
  registerAndConfirm,
  submitSignIn,
} from './support/account';
import { linkFromEmail } from './support/mailpit';

/** Convida um sócio pela tela e sai (o convite segue pelo Worker e pelo Mailpit). */
async function invitePartner(page: Page, email: string): Promise<void> {
  await page.getByRole('link', { name: 'Configurações' }).click();
  await page.getByRole('link', { name: 'Convidar pessoa' }).click();
  await page.getByLabel('E-mail').fill(email);
  await page.getByRole('combobox', { name: 'Perfil' }).click();
  await page.getByRole('option', { name: 'Sócio' }).click();
  await page.getByRole('button', { name: 'Enviar convite' }).click();
  await expect(page.getByRole('status')).toContainText(email);
  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page).toHaveURL(/\/entrar$/);
}

/**
 * Funil até o aceite pela tela (Etapa 4, PR 4D2): oportunidade com lead solto, cartão movido no kanban, proposta com
 * itens e total, aprovação interna recusada para quem elaborou e feita por um sócio, envio com o DOCX, aceite
 * bloqueado sem a verificação de conflito e, depois dela, o interessado vira cliente. Precisa do AppHost.
 */
test.describe('Funnel flow', () => {
  test.skip(
    !mailpitUrl,
    'Precisa do AppHost: defina E2E_MAILPIT_URL (ex.: http://localhost:8025).',
  );
  test.setTimeout(300_000);

  test('an opportunity goes from lead to an accepted proposal that creates the client', async ({
    page,
    request,
  }) => {
    const office = `Escritório Funil ${Date.now()}`;
    const partnerEmail = newEmail('socio');
    await createOfficeAsNewAdmin(page, request, office);

    // Oportunidade com o interessado em dados soltos.
    await page.getByRole('link', { name: 'Funil comercial' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Funil comercial' })).toBeVisible();
    await expectAccessible(page);
    await page.getByRole('link', { name: 'Nova oportunidade' }).click();
    await page.getByLabel('Título').fill('Contencioso trabalhista');
    await page.getByLabel('Nome ou razão social').fill('Construtora Horizonte S.A.');
    await page.getByLabel('CPF/CNPJ').fill('11.222.333/0001-81');
    await page.getByRole('combobox', { name: 'Origem' }).click();
    await page.getByRole('option', { name: 'Indicação de cliente' }).click();
    await page.getByLabel('Valor estimado').fill('60000');
    await page.getByRole('button', { name: 'Salvar' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Contencioso trabalhista' }),
    ).toBeVisible();
    await expect(page.getByText('Indicação de cliente')).toBeVisible();

    // No kanban, o cartão vai de lead para reunião pelo seletor (o mesmo que arrastar).
    await page.getByRole('link', { name: 'Voltar para o funil' }).click();
    const card = page.getByRole('article', { name: 'Contencioso trabalhista' });
    await card.getByRole('combobox', { name: 'Mover para' }).click();
    await page.getByRole('option', { name: 'Reunião' }).click();
    await expect(
      page
        .locator('section', { has: page.getByRole('heading', { name: 'Reunião' }) })
        .getByRole('link', { name: 'Contencioso trabalhista' }),
    ).toBeVisible();
    await expectAccessible(page);

    // Proposta com item mensal: total calculado e rascunho enviado para aprovação.
    await card.getByRole('link', { name: 'Contencioso trabalhista' }).click();
    await page.getByRole('link', { name: 'Nova proposta' }).click();
    await page.getByLabel('Título').fill('Proposta 2026');
    await page.getByLabel('Descrição').fill('Acompanhamento mensal');
    await page.getByRole('combobox', { name: 'Modalidade' }).click();
    await page.getByRole('option', { name: 'Mensal' }).click();
    await page.getByLabel('Quantidade').fill('12');
    await page.getByLabel('Valor', { exact: true }).fill('3500');
    await expect(page.getByText('Total: R$ 42.000,00')).toBeVisible();
    await page.getByRole('button', { name: 'Salvar rascunho' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Proposta 2026' })).toBeVisible();
    await expectAccessible(page);
    await page.getByRole('button', { name: 'Enviar para aprovação' }).click();
    await expect(page.getByText('Aguardando aprovação')).toBeVisible();

    // Quem elaborou não aprova.
    await page.getByRole('button', { name: 'Aprovar' }).click();
    await expect(page.getByRole('alert')).toContainText(
      'Quem elaborou a proposta não pode aprová-la.',
    );

    // Modelos de proposta nas configurações: o modelo padrão para baixar e adaptar.
    await page.getByRole('link', { name: 'Configurações' }).click();
    await page.getByRole('tab', { name: 'Modelos de proposta' }).click();
    await expect(
      page.getByRole('heading', { level: 2, name: 'Modelos de proposta' }),
    ).toBeVisible();
    await expectAccessible(page);
    const model = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Baixar o modelo padrão' }).click();
    expect((await model).suggestedFilename()).toBe('modelo-proposta-lexflow.docx');

    await invitePartner(page, partnerEmail);

    // O sócio cria a conta pelo convite, aceita e aprova pela fila do funil.
    await page.goto(await linkFromEmail(request, mailpitUrl!, partnerEmail, '/convite'));
    await page.getByRole('link', { name: 'Criar conta' }).click();
    await registerAndConfirm(page, request, 'Carlos Sócio', partnerEmail);
    await page.goto('/');
    await page.getByRole('button', { name: 'Entrar' }).click();
    await submitSignIn(page, partnerEmail);
    await page.getByRole('button', { name: 'Aceitar convite' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Início' })).toBeVisible();
    await page.getByRole('link', { name: 'Funil comercial' }).click();
    await page
      .getByRole('region', { name: 'Propostas aguardando sua aprovação' })
      .getByRole('link', { name: 'Proposta 2026' })
      .click();
    await page.getByRole('button', { name: 'Aprovar' }).click();
    await expect(page.getByText('Aprovada', { exact: true })).toBeVisible();

    // Envio: o DOCX fica congelado e pode ser baixado.
    await page.getByRole('button', { name: 'Registrar envio ao cliente' }).click();
    await expect(page.getByText('Enviada', { exact: true })).toBeVisible();
    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Baixar DOCX' }).click();
    expect((await download).suggestedFilename()).toBe('Proposta 2026.docx');

    // Sem a verificação de conflito, o aceite é recusado; com ela prosseguida, o interessado vira cliente.
    await page.getByRole('button', { name: 'Registrar aceite' }).click();
    await expect(page.getByRole('alert')).toContainText('Faça a verificação de conflito');
    await page
      .getByRole('link', { name: 'Ir para a verificação de conflito da oportunidade' })
      .click();
    await page.getByRole('button', { name: 'Verificar conflito' }).click();
    await expect(page.getByText('Sem conflito')).toBeVisible();
    await page.getByRole('button', { name: 'Prosseguir' }).click();
    await expect(page.getByRole('status')).toContainText('Verificação prosseguida');
    await expectAccessible(page);
    await page.getByRole('link', { name: 'Proposta 2026' }).click();
    await page.getByRole('button', { name: 'Registrar aceite' }).click();
    await expect(page.getByRole('status')).toContainText('o interessado agora é cliente');
    await page.getByRole('link', { name: 'Abrir o cadastro do cliente' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Construtora Horizonte S.A.' }),
    ).toBeVisible();
    await expect(page.getByText('Cliente', { exact: true })).toBeVisible();
  });
});
