import { writeFileSync } from 'node:fs';
import { expect, test } from '@playwright/test';
import { createOfficeAsNewAdmin, mailpitUrl } from '../support/account';

/**
 * Preparo do teste de carga da lista de processos (Etapa 5, PR 5B; `tests/LexFlow.LoadTests`). Cria pelo fluxo real
 * (cadastro, confirmação pelo Mailpit, login OIDC com PKCE) uma conta com escritório próprio, cadastra duas áreas e um
 * cliente, e grava em `LOAD_OUTPUT` o token de acesso e os ids que o seed e o k6 usam. Só roda com `LOAD_OUTPUT`
 * definido (fica fora da suíte e2e).
 */
const output = process.env['LOAD_OUTPUT'];

test.describe('Load test preparation', () => {
  test.skip(!output || !mailpitUrl, 'Só no teste de carga: defina LOAD_OUTPUT e E2E_MAILPIT_URL.');
  test.setTimeout(180_000);

  test('creates the load office and saves the access token', async ({ page, request }) => {
    await createOfficeAsNewAdmin(page, request, `Escritório Carga ${Date.now()}`);

    const session = await page.evaluate(() => {
      const values = Object.keys(sessionStorage).map((key) => sessionStorage.getItem(key) ?? '');
      const token = values
        .map((value) => {
          try {
            return (JSON.parse(value) as { authnResult?: { access_token?: string } }).authnResult
              ?.access_token;
          } catch {
            return undefined;
          }
        })
        .find((value) => !!value);
      return { token, organizationId: sessionStorage.getItem('lexflow.activeOrganization') };
    });
    expect(session.token).toBeTruthy();
    expect(session.organizationId).toBeTruthy();

    const config = (await (await request.get('/config.json')).json()) as { apiUrl: string };
    const headers = {
      Authorization: `Bearer ${session.token}`,
      'X-Organization-Id': session.organizationId!,
    };
    const create = async (path: string, data: object): Promise<string> => {
      const response = await request.post(`${config.apiUrl}${path}`, { headers, data });
      expect(response.status(), await response.text()).toBe(201);
      return ((await response.json()) as { id: string }).id;
    };

    // O token de acesso é criptografado (JWE): o id do usuário vem do próprio perfil.
    const me = (await (await request.get(`${config.apiUrl}/api/v1/me`, { headers })).json()) as {
      id: string;
    };
    const areaId = await create('/api/v1/organization/practice-areas', { name: 'Trabalhista' });
    const otherAreaId = await create('/api/v1/organization/practice-areas', { name: 'Cível' });
    const clientId = await create('/api/v1/people', {
      kind: 'company',
      name: 'Cliente da Carga S.A.',
      roles: ['client'],
    });

    writeFileSync(
      output!,
      JSON.stringify(
        {
          apiUrl: config.apiUrl,
          token: session.token,
          organizationId: session.organizationId,
          userId: me.id,
          areaId,
          otherAreaId,
          clientId,
        },
        null,
        2,
      ),
    );
  });
});
