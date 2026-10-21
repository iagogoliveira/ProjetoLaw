import { expect, test } from '@playwright/test';
import { createOfficeAsNewAdmin, expectAccessible, mailpitUrl } from './support/account';
import { createSimpleCase, openCaseTab } from './support/cases';

const futureDay = () => {
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(
    new Date(),
  );
  const value = new Date(`${today}T12:00:00Z`);
  value.setUTCDate(value.getUTCDate() + 1);
  return value.toISOString().slice(0, 10);
};

test.describe('Deadline lifecycle flow', () => {
  test.skip(!mailpitUrl, 'Precisa do AppHost com Worker e Mailpit.');
  test.setTimeout(300_000);
  test.use({ actionTimeout: 20_000, navigationTimeout: 45_000 });

  test('missed deadline reaches its case through notifications and the timeline', async ({
    page,
    request,
  }) => {
    await createOfficeAsNewAdmin(page, request, `Prazo ${Date.now()}`);
    const [caseResponse] = await Promise.all([
      page.waitForResponse(
        (response) =>
          response.url().endsWith('/api/v1/cases') && response.request().method() === 'POST',
      ),
      createSimpleCase(page),
    ]);
    expect(caseResponse.status()).toBe(201);
    const caseId = (await caseResponse.json()).id as string;
    const headers = Object.fromEntries(
      Object.entries(await caseResponse.request().allHeaders()).filter(([key]) =>
        ['authorization', 'x-organization-id'].includes(key.toLowerCase()),
      ),
    );
    const origin = new URL(caseResponse.url()).origin;

    const optionsResponse = await page.request.get(`${origin}/api/v1/cases/options`, { headers });
    expect(optionsResponse.status()).toBe(200);
    const options = await optionsResponse.json();
    expect(options.members).toHaveLength(1);
    const assigneeUserId = options.members[0].userId as string;

    const triggerDate = futureDay();
    const rule = {
      startKind: 'publication',
      triggerDate,
      consultedOn: null,
      days: 5,
      mode: 'businessDays',
      doubled: false,
      internalLeadDays: 1,
    };
    const calculationResponse = await page.request.post(`${origin}/api/v1/deadlines/calculate`, {
      headers,
      data: {
        caseId,
        courtId: null,
        stateCode: null,
        municipality: null,
        ...rule,
      },
    });
    expect(calculationResponse.status()).toBe(200);
    const calculation = await calculationResponse.json();

    const createDeadlineResponse = await page.request.post(
      `${origin}/api/v1/cases/${caseId}/deadlines`,
      {
        headers,
        data: {
          type: 'procedural',
          title: 'Prazo E2E de teste',
          assigneeUserId,
          rule,
          confirmedInternalDueOn: calculation.internalDueOn,
          confirmedFatalDueOn: calculation.fatalDueOn,
          confirmed: true,
        },
      },
    );
    expect(createDeadlineResponse.status()).toBe(201);
    const deadline = await createDeadlineResponse.json();

    const missResponse = await page.request.post(`${origin}/api/v1/deadlines/${deadline.id}/miss`, {
      headers,
      data: { reason: 'Justificativa registrada no teste E2E.' },
    });
    expect(missResponse.status()).toBe(200);

    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Início', exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Notificações', exact: true }).click();
    const panel = page.getByRole('region', { name: 'Notificações', exact: true });
    await expect(panel.getByText('Prazo perdido', { exact: true })).toBeVisible({
      timeout: 45_000,
    });
    await expectAccessible(page);
    const caseLink = panel.getByRole('link', { name: 'Abrir processo', exact: true });
    await expect(caseLink).toHaveAttribute('href', `/processos/${caseId}`);
    await caseLink.click();
    await expect.poll(() => new URL(page.url()).pathname).toBe(`/processos/${caseId}`);

    await openCaseTab(page, 'Linha do tempo');
    await expect(page.getByRole('listitem').filter({ hasText: 'Prazo perdido' })).toHaveCount(1);
  });
});
