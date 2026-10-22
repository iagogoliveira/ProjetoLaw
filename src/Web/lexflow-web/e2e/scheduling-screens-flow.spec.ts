import { expect, test, type Page } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';
import { choose } from './support/cases';

const today = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date());
async function changeTaskState(page: Page, id: string, label: string): Promise<void> {
  const [response] = await Promise.all([
    page.waitForResponse(
      (r) => r.url().endsWith(`/tasks/${id}/state`) && r.request().method() === 'POST',
    ),
    choose(page, 'Mover para', label),
  ]);
  expect(response.status()).toBe(200);
}
test.describe('Agenda, tasks and control screens', () => {
  test.skip(!mailpitUrl, 'Precisa do AppHost com Worker e Mailpit.');
  test.setTimeout(240_000);
  test('administrative tasks enforce dependencies and support reopening, list and keyboard kanban', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, 'Escritório Tarefas 7F');
    await page.goto('/tarefas/novo');
    await page.getByLabel('Título', { exact: true }).fill('Organizar documentação administrativa');
    await page.getByLabel('Vencimento', { exact: true }).fill(today());
    await choose(page, 'Responsável', 'Ana Administradora');
    await choose(page, 'Prioridade', 'Alta');
    await expectAccessible(page);
    const [created] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith('/api/v1/tasks') && r.request().method() === 'POST',
      ),
      page.getByRole('button', { name: 'Salvar', exact: true }).click(),
    ]);
    expect(created.status()).toBe(201);
    const task = await created.json();
    expect(task.caseId).toBeNull();
    await expect(page.getByRole('heading', { name: task.title, exact: true })).toBeVisible();
    const checklist = page.getByRole('heading', { name: 'Checklist', exact: true }).locator('..');
    await checklist.getByLabel('Novo item', { exact: true }).fill('Conferir documentos');
    await checklist.getByRole('button', { name: 'Adicionar', exact: true }).click();
    await expect(
      page.getByRole('checkbox', { name: 'Conferir documentos', exact: true }),
    ).toBeVisible();
    const comments = page.getByRole('heading', { name: 'Comentários', exact: true }).locator('..');
    await comments
      .getByLabel('Novo comentário', { exact: true })
      .fill('Revisão administrativa em andamento');
    await comments.getByRole('button', { name: 'Adicionar', exact: true }).click();
    await expect(
      page.getByText('Revisão administrativa em andamento', { exact: true }),
    ).toBeVisible();
    const children = page.getByRole('heading', { name: 'Subtarefas', exact: true }).locator('..');
    await children.getByLabel('Título', { exact: true }).fill('Revisar anexo');
    await children.getByLabel('Vencimento', { exact: true }).fill(today());
    await choose(page, 'Responsável', 'Ana Administradora');
    await children.getByRole('button', { name: 'Adicionar subtarefa', exact: true }).click();
    await expect(page.getByRole('link', { name: 'Revisar anexo', exact: true })).toBeVisible();
    const [blocked] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith(`/tasks/${task.id}/state`) && r.request().method() === 'POST',
      ),
      choose(page, 'Mover para', 'Concluída'),
    ]);
    expect(blocked.status()).toBe(422);
    expect((await blocked.json()).code).toBe('scheduling.task_unresolved');
    await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/checklist/') && r.request().method() === 'PUT',
      ),
      page.getByRole('checkbox', { name: 'Conferir documentos', exact: true }).check(),
    ]);
    await page.getByRole('link', { name: 'Revisar anexo', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Revisar anexo', exact: true })).toBeVisible();
    const childId = new URL(page.url()).pathname.split('/').at(-1)!;
    await changeTaskState(page, childId, 'Cancelada');
    await page.goto(`/tarefas/${task.id}`);
    await expect(page.getByRole('heading', { name: task.title, exact: true })).toBeVisible();
    await changeTaskState(page, task.id, 'Concluída');
    await expect(page.getByRole('combobox', { name: 'Mover para', exact: true })).toContainText(
      'Concluída',
    );
    await changeTaskState(page, task.id, 'Pendente');
    await expectAccessible(page);
    await page.goto('/tarefas');
    await page.getByRole('button', { name: 'Lista', exact: true }).click();
    await expect(page.getByRole('table')).toBeVisible();
    await expect(page.getByRole('link', { name: task.title, exact: true })).toBeVisible();
    await expectAccessible(page);
    await page
      .getByRole('row')
      .filter({ has: page.getByRole('link', { name: task.title, exact: true }) })
      .getByRole('checkbox')
      .check();
    await page
      .getByRole('row')
      .filter({ has: page.getByRole('link', { name: 'Revisar anexo', exact: true }) })
      .getByRole('checkbox')
      .check();
    await choose(page, 'Novo responsável', 'Ana Administradora');
    const [redistributed] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith('/tasks/redistribute') && r.request().method() === 'POST',
      ),
      page.getByRole('button', { name: 'Redistribuir selecionadas', exact: true }).click(),
    ]);
    expect(redistributed.status()).toBe(200);
    const outcomes = (await redistributed.json()).items as { changed: boolean }[];
    expect(outcomes.filter((item) => item.changed)).toHaveLength(1);
    expect(outcomes.filter((item) => !item.changed)).toHaveLength(1);
    await expect(
      page.getByRole('heading', { name: 'Resultado por tarefa', exact: true }),
    ).toBeVisible();
    await page.getByRole('button', { name: 'Kanban', exact: true }).click();
    await expect(
      page
        .locator('article')
        .filter({ has: page.getByRole('link', { name: 'Revisar anexo', exact: true }) })
        .getByRole('combobox', { name: 'Mover para', exact: true }),
    ).toHaveValue('cancelled');
    const card = page
      .locator('article')
      .filter({ has: page.getByRole('link', { name: task.title, exact: true }) });
    await expect(card.getByRole('combobox', { name: 'Mover para', exact: true })).toHaveValue(
      'pending',
    );
    await card
      .getByRole('combobox', { name: 'Mover para', exact: true })
      .selectOption('inProgress');
    await expect(card.getByRole('combobox', { name: 'Mover para', exact: true })).toHaveValue(
      'inProgress',
    );
    await expectAccessible(page);
  });

  test('appointments retain São Paulo time and agenda supports periods, saved filters and cancellation', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, 'Escritório Agenda 7F');
    await page.goto('/agenda/compromissos/novo');
    await page.getByLabel('Título', { exact: true }).fill('Reunião administrativa');
    await page.getByLabel('Local', { exact: true }).fill('Sala de reuniões');
    await page.getByLabel('Início', { exact: true }).fill(`${today()}T09:30`);
    await page.getByLabel('Fim', { exact: true }).fill(`${today()}T10:30`);
    await choose(page, 'Responsável', 'Ana Administradora');
    const [created] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith('/api/v1/schedule/events') && r.request().method() === 'POST',
      ),
      page.getByRole('button', { name: 'Salvar compromisso', exact: true }).click(),
    ]);
    expect(created.status()).toBe(201);
    const event = await created.json();
    expect(event.caseId).toBeNull();
    expect(new Date(event.startsAt).toISOString()).toBe(`${today()}T12:30:00.000Z`);
    await expect(page).toHaveURL(new RegExp(`/agenda/compromissos/${event.id}$`));
    await expect(page.getByLabel('Início', { exact: true })).toHaveValue(`${today()}T09:30`);
    await expectAccessible(page);
    await page.getByRole('link', { name: 'Voltar à agenda', exact: true }).click();
    await expect(
      page.getByRole('link', { name: 'Reunião administrativa', exact: true }),
    ).toBeVisible();
    for (const view of ['Dia', 'Mês', 'Semana']) {
      await page.getByRole('button', { name: view, exact: true }).click();
      await expect(page.getByRole('button', { name: view, exact: true })).toHaveAttribute(
        'aria-pressed',
        'true',
      );
      await expect(
        page.getByRole('link', { name: 'Reunião administrativa', exact: true }),
      ).toBeVisible();
    }
    await expectAccessible(page);
    await page.getByLabel('Nome do filtro da agenda', { exact: true }).fill('Minha agenda');
    await page.getByRole('button', { name: 'Salvar filtro da agenda', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Minha agenda', exact: true })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: 'Minha agenda', exact: true })).toBeVisible();
    await page.emulateMedia({ colorScheme: 'dark' });
    await expectAccessible(page);
    await page.emulateMedia({ colorScheme: 'light' });
    await page.getByRole('link', { name: 'Reunião administrativa', exact: true }).click();
    await page
      .getByRole('checkbox', { name: /Confirmo que quero cancelar este compromisso/ })
      .check();
    await page.getByRole('button', { name: 'Cancelar compromisso', exact: true }).click();
    await expect(page.getByText('Cancelado', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Salvar compromisso', exact: true })).toHaveCount(
      0,
    );
    await expectAccessible(page);
  });

  test('notification choices survive focus and reload while mandatory rules remain enforced', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, 'Escritório Controladoria 7F');
    await page.goto('/notificacoes/preferencias');
    await page
      .getByRole('checkbox', { name: 'Receber resumo diário por e-mail', exact: true })
      .check();
    await page.getByRole('checkbox', { name: 'Agenda', exact: true }).check();
    await page.getByRole('checkbox', { name: 'Prazos', exact: true }).check();
    const [saved] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().endsWith('/me/notification-settings') && r.request().method() === 'PUT',
      ),
      page.getByRole('button', { name: 'Salvar preferências pessoais', exact: true }).click(),
    ]);
    expect(saved.status()).toBe(200);
    expect((await saved.json()).dailyDigestEnabled).toBe(true);
    await page.reload();
    await expect(
      page.getByRole('checkbox', { name: 'Receber resumo diário por e-mail', exact: true }),
    ).toBeChecked();
    await expect(
      page.getByText('Prazo perdido é obrigatório pelo sistema.', { exact: true }),
    ).toBeVisible();
    await expectAccessible(page);
    await page.goto('/controladoria');
    await expect(page.getByRole('heading', { name: 'Controladoria', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Indicadores', exact: true })).toBeVisible();
    await page.getByLabel('Dias sem andamento', { exact: true }).fill('45');
    await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
    await expect(page).toHaveURL(/idleDays=45/);
    await expectAccessible(page);
    await page.getByLabel('Nome do conjunto', { exact: true }).fill('Controle mensal');
    await page.getByRole('button', { name: 'Salvar conjunto', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Controle mensal', exact: true })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: 'Controle mensal', exact: true })).toBeVisible();
    await expectAccessible(page);
  });
});
