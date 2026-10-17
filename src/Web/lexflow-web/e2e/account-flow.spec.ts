import { expect, test } from '@playwright/test';
import {
  expectAccessible,
  mailpitUrl,
  newEmail,
  registerAndConfirm,
  signInWithPassword,
} from './support/account';
import { totp } from './support/totp';

/**
 * Critério de aceite da Etapa 2: cadastro → confirmação de e-mail (lida no Mailpit) → login → ativação de TOTP →
 * logout → login com TOTP. Precisa do ambiente completo (AppHost): API, Worker, banco e Mailpit. Sem escritório, o
 * login leva ao onboarding (Etapa 3), mas "Minha conta" continua acessível.
 */
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
    const email = newEmail('e2e');

    // Cadastro na página do servidor de identidade e confirmação pelo e-mail enviado pelo Worker.
    await page.goto('/entrar');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Bem-vindo ao LexFlow' }),
    ).toBeVisible();
    await expectAccessible(page);
    await page.getByRole('link', { name: 'Criar conta' }).click();
    await registerAndConfirm(page, request, 'Ana E2E', email);

    // Login: volta ao SPA, no shell; sem escritório, cai no onboarding.
    await signInWithPassword(page, email);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Crie seu escritório' }),
    ).toBeVisible();
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
    await signInWithPassword(page, email);
    await expect(page).toHaveURL(/\/conta\/segundo-fator/);
    await page.getByLabel('Código').fill(totp(key));
    await page.getByRole('button', { name: 'Confirmar' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Crie seu escritório' }),
    ).toBeVisible();
  });
});
