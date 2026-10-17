import AxeBuilder from '@axe-core/playwright';
import { expect, Page, test } from '@playwright/test';
import { linkFromEmail } from './support/mailpit';
import { totp } from './support/totp';

/**
 * Critério de aceite da Etapa 2: cadastro → confirmação de e-mail (lida no Mailpit) → login → ativação de TOTP →
 * logout → login com TOTP. Precisa do ambiente completo (AppHost): API, Worker, banco e Mailpit.
 */
const mailpitUrl = process.env['E2E_MAILPIT_URL'];

async function expectAccessible(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(results.violations).toEqual([]);
}

async function signInWithPassword(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/');
  await expect(page).toHaveURL(/\/entrar$/);
  await page.getByRole('button', { name: 'Entrar' }).click();

  await expect(page).toHaveURL(/\/conta\/entrar/);
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill(password);
  await page.getByRole('button', { name: 'Entrar' }).click();
}

test.describe('Account flow', () => {
  test.skip(
    !mailpitUrl,
    'Precisa do AppHost: defina E2E_MAILPIT_URL (ex.: http://localhost:8025).',
  );
  test.setTimeout(180_000);

  test('register, confirm email, sign in, turn on TOTP, sign out and sign in with TOTP', async ({
    page,
    request,
  }) => {
    const email = `e2e-${Date.now()}@exemplo.com.br`;
    const password = 'uma senha bem longa para o e2e';

    // Cadastro na página do servidor de identidade.
    await page.goto('/entrar');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Bem-vindo ao LexFlow' }),
    ).toBeVisible();
    await expectAccessible(page);
    await page.getByRole('link', { name: 'Criar conta' }).click();
    await page.getByLabel('Nome completo').fill('Ana E2E');
    await page.getByLabel('E-mail').fill(email);
    await page.getByLabel('Senha').fill(password);
    await page.getByRole('button', { name: 'Criar conta' }).click();
    await expect(page.getByRole('status')).toContainText('Enviamos um e-mail');

    // Confirmação pelo link do e-mail enviado pelo Worker.
    await page.goto(await linkFromEmail(request, mailpitUrl!, email, '/conta/confirmar-email'));
    await expect(page.getByRole('status')).toContainText('E-mail confirmado');

    // Login: volta ao SPA, no shell.
    await signInWithPassword(page, email, password);
    await expect(page.getByRole('heading', { level: 1, name: 'Início' })).toBeVisible();
    await expect(page.getByRole('banner')).toContainText('Ana E2E');
    await expectAccessible(page);

    // Ativação do TOTP em "Minha conta".
    await page.getByRole('link', { name: 'Minha conta' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Minha conta' })).toBeVisible();
    await expect(page.getByText(email)).toBeVisible();
    await page.getByRole('button', { name: 'Usar aplicativo autenticador' }).click();
    const key = (await page.getByTestId('totp-key').textContent())!.trim();
    await expectAccessible(page);
    await page.getByLabel('Código de 6 dígitos').fill(totp(key));
    await page.getByRole('button', { name: 'Confirmar e ativar' }).click();
    await expect(page.getByRole('heading', { name: 'Códigos de recuperação' })).toBeVisible();
    await expect(page.getByRole('status')).toContainText('aplicativo autenticador');

    // Logout: a sessão é encerrada no servidor e o SPA volta para /entrar.
    await page.getByRole('button', { name: 'Sair' }).click();
    await expect(page).toHaveURL(/\/entrar$/);

    // Novo login, agora com o segundo fator.
    await signInWithPassword(page, email, password);
    await expect(page).toHaveURL(/\/conta\/segundo-fator/);
    await page.getByLabel('Código').fill(totp(key));
    await page.getByRole('button', { name: 'Confirmar' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Início' })).toBeVisible();
  });
});
