import { expect, Page, test } from '@playwright/test';
import {
  createOfficeAsNewAdmin,
  expectAccessible,
  mailpitUrl,
  newEmail,
  registerAndConfirm,
  submitSignIn,
} from './support/account';
import { choose, createSimpleCase, openCaseTab } from './support/cases';
import { linkFromEmail } from './support/mailpit';

const day = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date());
const futureDay = (offset: number) => {
  const value = new Date(day() + 'T12:00:00Z');
  value.setUTCDate(value.getUTCDate() + offset);
  return value.toISOString().slice(0, 10);
};
async function firstOption(page: Page, label: string): Promise<void> {
  const field = page.getByRole('combobox', { name: label, exact: true });
  await field.focus();
  await field.press('Enter');
  await page.getByRole('option').filter({ hasNotText: 'Sem processo' }).first().click();
}
async function createSource(page: Page, label: string) {
  await page.goto('/publicacoes/fontes');
  await page.getByLabel('Nome da fonte', { exact: true }).fill(label);
  await page.getByLabel('Valor monitorado', { exact: true }).fill(label);
  const [response] = await Promise.all([
    page.waitForResponse(
      (r) => r.url().endsWith('/api/v1/publication-sources') && r.request().method() === 'POST',
    ),
    page.getByRole('button', { name: 'Salvar fonte', exact: true }).click(),
  ]);
  expect(response.status()).toBe(201);
  await page.getByRole('button', { name: `Consultar captura de ${label}`, exact: true }).click();
  await page.getByRole('button', { name: 'Tentar captura agora', exact: true }).click();
  await expect(page.getByText(/Solicitação de captura registrada/)).toBeVisible();
  return response;
}
async function selectFirst(page: Page): Promise<void> {
  await page
    .getByRole('navigation', { name: 'Fila de publicações' })
    .getByRole('button')
    .first()
    .click();
  await expect(
    page.getByRole('heading', { level: 3, name: 'Triagem da publicação' }),
  ).toBeVisible();
}
async function submitTriage(page: Page, action: string) {
  await page.getByRole('checkbox', { name: /Li a publicação e confirmo/ }).check();
  const [response] = await Promise.all([
    page.waitForResponse((r) => r.url().endsWith('/triage') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Confirmar e salvar triagem', exact: true }).click(),
  ]);
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.triage.action).toBe(action);
  await expect(page.getByText('Operação salva.', { exact: true })).toBeVisible();
  return response;
}
async function discard(page: Page): Promise<void> {
  await selectFirst(page);
  await choose(page, 'Classificação', 'Outros');
  await choose(page, 'Ação da triagem', 'Encerrar como irrelevante/engano');
  await page.getByLabel('Justificativa da triagem').fill('Homônimo conferido pela controladoria');
  await submitTriage(page, 'discard');
}
async function closeDay(page: Page): Promise<void> {
  await page.goto('/publicacoes/fechamento');
  await expect(page.getByText('Dia ainda exige fechamento.', { exact: true })).toBeVisible();
  await page.getByRole('checkbox', { name: /Conferi o dia, as triagens/ }).check();
  await expect(
    page.getByRole('button', { name: 'Confirmar fechamento', exact: true }),
  ).toBeEnabled();
  await page.getByRole('button', { name: 'Confirmar fechamento', exact: true }).click();
  await expect(page.getByText('Fechamento vigente.', { exact: true })).toBeVisible();
}

test.describe('Publications flow', () => {
  test.skip(!mailpitUrl, 'Precisa do AppHost com Worker e Mailpit.');
  test.setTimeout(300_000);
  test.use({ actionTimeout: 20000, navigationTimeout: 45000 });
  test('capture, live queue, assignment, triage destinations, movements and closing preserve the audit history', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, `Publicações ${Date.now()}`);
    await createSimpleCase(page);
    const casePath = new URL(page.url()).pathname;
    const source = await createSource(page, 'Fonte Principal');
    const headers = Object.fromEntries(
      Object.entries(await source.request().allHeaders()).filter(([key]) =>
        ['authorization', 'x-organization-id'].includes(key),
      ),
    );
    const origin = new URL(source.url()).origin;
    await expectAccessible(page);
    // A chave é enviada sem eco e limpa; o caso extrajudicial não dispara consultas externas ao CNJ.
    await choose(page, 'Provedor de andamentos', 'DataJud (CNJ)');
    await page.getByLabel('Nova chave da API DataJud').fill('e2e-placeholder-key');
    await page.getByRole('button', { name: 'Salvar integração de andamentos' }).click();
    await expect(
      page.getByText('Há uma chave protegida cadastrada.', { exact: true }),
    ).toBeVisible();
    await expect(page.getByLabel('Nova chave da API DataJud')).toHaveValue('');
    await choose(page, 'Provedor de andamentos', 'Desabilitado');
    await page.getByRole('checkbox', { name: 'Remover a chave cadastrada' }).check();
    await page.getByRole('button', { name: 'Salvar integração de andamentos' }).click();
    await expect(page.getByText('Nenhuma chave cadastrada.', { exact: true })).toBeVisible();
    await page.goto('/publicacoes');
    const rows = page.getByRole('navigation', { name: 'Fila de publicações' }).getByRole('button');
    await expect(rows).toHaveCount(2, { timeout: 45000 });
    await expect(page.getByRole('link', { name: /Publicações.*2/ })).toBeVisible();
    await choose(page, 'Vínculo com processo', 'Não vinculadas');
    await page.getByRole('button', { name: 'Aplicar filtros' }).click();
    await expect(page).toHaveURL(/linked=false/);
    await selectFirst(page);
    const selected = new URL(page.url()).searchParams.get('publication');
    await page.keyboard.press('j');
    await expect.poll(() => new URL(page.url()).searchParams.get('publication')).not.toBe(selected);
    await page.keyboard.press('k');
    await expect.poll(() => new URL(page.url()).searchParams.get('publication')).toBe(selected);
    await expect(
      page.getByRole('heading', { name: 'Triagem da publicação', exact: true }),
    ).toBeVisible();
    await choose(page, 'Atribuir triagem a', 'Ana Administradora');
    await page.getByRole('button', { name: 'Salvar distribuição' }).click();
    await firstOption(page, 'Processo da triagem');
    await page.getByRole('button', { name: 'Vincular processo', exact: true }).click();
    // Sai da fila não vinculada após o vínculo; o filtro compartilhável é removido explicitamente.
    await choose(page, 'Vínculo com processo', 'Todas');
    await page.getByRole('button', { name: 'Aplicar filtros' }).click();
    await selectFirst(page);
    await choose(page, 'Classificação', 'Intimação com prazo');
    await choose(page, 'Ação da triagem', 'Criar prazo');
    await page.getByLabel('Título do prazo ou tarefa').fill('Contestação confirmada');
    const selectedBeforeTyping = new URL(page.url()).searchParams.get('publication');
    await page.getByLabel('Título do prazo ou tarefa').press('j');
    await expect
      .poll(() => new URL(page.url()).searchParams.get('publication'))
      .toBe(selectedBeforeTyping);
    await page.getByLabel('Título do prazo ou tarefa').fill('Contestação confirmada');
    // Outra captura chega durante a edição: SignalR avisa e o formulário continua preenchido.
    const added = await page.request.post(`${origin}/api/v1/publication-sources`, {
      headers,
      data: { kind: 'name', label: 'Fonte Novidades', value: 'Fonte Novidades', isActive: true },
    });
    expect(added.status()).toBe(201);
    const addedSource = await added.json();
    expect(
      (
        await page.request.post(`${origin}/api/v1/publication-sources/${addedSource.id}/capture`, {
          headers,
          data: { day: day() },
        })
      ).status(),
    ).toBe(202);
    await expect(page.getByRole('link', { name: /Publicações.*4/ })).toBeVisible({
      timeout: 45000,
    });
    await expect(page.getByText(/A fila pode ter novidades/)).toBeVisible();
    await expect(page.getByLabel('Título do prazo ou tarefa')).toHaveValue(
      'Contestação confirmada',
    );
    await firstOption(page, 'Processo da triagem');
    await choose(page, 'Responsável pelo destino', 'Ana Administradora');
    await page.getByLabel('Início do prazo', { exact: true }).fill(day());
    await page.getByLabel('Vencimento interno', { exact: true }).fill(futureDay(10));
    await page.getByLabel('Vencimento fatal', { exact: true }).fill(futureDay(15));
    await expectAccessible(page);
    const triaged = await submitTriage(page, 'deadline');
    const result = await triaged.json();
    expect(result.triage.deadlineId).toBeTruthy();
    const deadlines = await page.request.get(
      `${origin}/api/v1/cases/${result.triage.caseId}/deadlines`,
      { headers },
    );
    expect((await deadlines.json())[0].id).toBe(result.triage.deadlineId);
    await page.goto('/publicacoes/fechamento');
    await expect(page.getByRole('button', { name: 'Confirmar fechamento' })).toBeDisabled();
    await expectAccessible(page);
    await page.goto('/publicacoes');
    await discard(page);
    await selectFirst(page);
    await firstOption(page, 'Processo da triagem');
    await choose(page, 'Ação da triagem', 'Criar tarefa');
    await page.getByLabel('Título do prazo ou tarefa').fill('Conferir documentos');
    await choose(page, 'Responsável pelo destino', 'Ana Administradora');
    await page.getByLabel('Data da tarefa', { exact: true }).fill(futureDay(12));
    expect((await (await submitTriage(page, 'task')).json()).triage.taskId).toBeTruthy();
    await selectFirst(page);
    await firstOption(page, 'Processo da triagem');
    await choose(page, 'Classificação', 'Audiência');
    await choose(page, 'Ação da triagem', 'Criar audiência');
    await choose(page, 'Responsável pelo destino', 'Ana Administradora');
    await page.getByLabel('Data e hora da audiência').fill(futureDay(20) + 'T14:00');
    await page.getByLabel('Local da audiência').fill('Fórum Central');
    expect((await (await submitTriage(page, 'hearing')).json()).triage.hearingId).toBeTruthy();
    await closeDay(page);
    await expect(page.getByRole('listitem').filter({ hasText: 'Revisão 1' })).toHaveCount(1);
    await expectAccessible(page);
    await createSource(page, 'Fonte Posterior');
    await page.goto('/publicacoes');
    await expect(rows).toHaveCount(2, { timeout: 45000 });
    await discard(page);
    await discard(page);
    await closeDay(page);
    await expect(page.getByRole('listitem').filter({ hasText: /Revisão [12]/ })).toHaveCount(2);
    await page.goto(casePath);
    await openCaseTab(page, 'Andamentos');
    await page.getByLabel('Data e hora do andamento').fill(day() + 'T10:00');
    await page.getByLabel('Descrição do andamento').fill('Juntada manual conferida');
    await page.getByRole('button', { name: 'Salvar andamento', exact: true }).click();
    await expect(
      page.getByRole('listitem').filter({ hasText: 'Juntada manual conferida' }),
    ).toHaveCount(1);
    await expectAccessible(page);
    await openCaseTab(page, 'Linha do tempo');
    await expect(
      page.getByRole('listitem').filter({ hasText: 'Andamento: Juntada manual conferida' }),
    ).toHaveCount(1);
    await page.getByRole('button', { name: 'Notificações', exact: true }).click();
    await expect(
      page.getByText('Atualização em tempo real conectada.', { exact: true }),
    ).toBeVisible({ timeout: 15000 });
    await expectAccessible(page);
  });

  test('a publication reader cannot triage, distribute, close or manage sources', async ({
    page,
    request,
  }) => {
    const office = `Leitura Publicações ${Date.now()}`;
    await createOfficeAsNewAdmin(page, request, office);
    await createSource(page, 'Fonte Leitura');
    await page.getByRole('link', { name: 'Configurações', exact: true }).click();
    await page.getByRole('tab', { name: 'Perfis', exact: true }).click();
    await page.getByLabel('Nome do novo perfil').fill('Leitura Publicações');
    await page.getByRole('button', { name: 'Criar perfil' }).click();
    await expect(page.getByLabel('Nome', { exact: true })).toHaveValue('Leitura Publicações');
    await page.getByRole('checkbox', { name: 'Ver publicações e intimações', exact: true }).check();
    await page.getByRole('button', { name: 'Salvar permissões' }).click();
    await expect(page.getByRole('status')).toContainText('Alterações salvas.');
    await page.getByRole('tab', { name: 'Usuários e convites', exact: true }).click();
    await page.getByRole('link', { name: 'Convidar pessoa', exact: true }).click();
    const email = newEmail('publications-reader');
    await page.getByLabel('E-mail').fill(email);
    await choose(page, 'Perfil', 'Leitura Publicações');
    await choose(page, 'Escopo de dados', 'Todo o escritório');
    await page.getByRole('button', { name: 'Enviar convite' }).click();
    await expect(page.getByRole('status')).toContainText(email);
    await page.getByRole('button', { name: 'Sair', exact: true }).click();
    await page.goto(await linkFromEmail(request, mailpitUrl!, email, '/convite'));
    await page.getByRole('link', { name: 'Criar conta' }).click();
    await registerAndConfirm(page, request, 'Leitora Publicações', email);
    await page.goto('/');
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await submitSignIn(page, email);
    await page.getByRole('button', { name: 'Aceitar convite' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Início', exact: true }),
    ).toBeVisible();
    await page.goto('/publicacoes');
    await expect(
      page.getByRole('navigation', { name: 'Fila de publicações' }).getByRole('button'),
    ).toHaveCount(2, { timeout: 45000 });
    await page
      .getByRole('navigation', { name: 'Fila de publicações' })
      .getByRole('button')
      .first()
      .click();
    await expect(page.getByRole('heading', { level: 2, name: 'Diário simulado' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Confirmar e salvar triagem' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Salvar distribuição' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Fontes e integrações', exact: true })).toHaveCount(
      0,
    );
    await expect(page.getByRole('link', { name: 'Fechamento diário', exact: true })).toHaveCount(0);
    await expectAccessible(page);
    await page.goto('/publicacoes/fontes');
    await expect(page).not.toHaveURL(/\/publicacoes\/fontes/);
    await page.goto('/publicacoes/fechamento');
    await expect(page).not.toHaveURL(/\/publicacoes\/fechamento/);
  });
});
