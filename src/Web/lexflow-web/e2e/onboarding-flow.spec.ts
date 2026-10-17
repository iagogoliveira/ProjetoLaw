import { expect, test } from '@playwright/test';
import {
  expectAccessible,
  mailpitUrl,
  newEmail,
  registerAndConfirm,
  signInWithPassword,
  submitSignIn,
} from './support/account';
import { linkFromEmail } from './support/mailpit';

/**
 * E2E do onboarding (seção 13.2, Etapa 3): cadastro → criar escritório → convidar advogado → o advogado, ainda sem
 * conta, abre o convite, cria a conta com o e-mail convidado, confirma, entra e aceita. Precisa do AppHost (API,
 * Worker e Mailpit).
 */
test.describe('Onboarding flow', () => {
  test.skip(
    !mailpitUrl,
    'Precisa do AppHost: defina E2E_MAILPIT_URL (ex.: http://localhost:8025).',
  );
  test.setTimeout(240_000);

  test('register, create the office, invite a lawyer who signs up and accepts', async ({
    page,
    request,
  }) => {
    const adminEmail = newEmail('admin');
    const lawyerEmail = newEmail('advogada');
    const office = `Escritório E2E ${Date.now()}`;

    // Quem cria o escritório: cadastro, confirmação e primeiro login, que leva ao onboarding.
    await page.goto('/entrar');
    await page.getByRole('link', { name: 'Criar conta' }).click();
    await registerAndConfirm(page, request, 'Ana Administradora', adminEmail);
    await signInWithPassword(page, adminEmail);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Crie seu escritório' }),
    ).toBeVisible();
    await expectAccessible(page);
    await page.getByLabel('Nome do escritório').fill(office);
    await page.getByRole('button', { name: 'Criar escritório' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Início' })).toBeVisible();
    await expect(page.getByTestId('active-organization')).toHaveText(office);

    // Convite para a advogada, com o perfil Advogado.
    await page.getByRole('link', { name: 'Configurações' }).click();
    await expect(page.getByRole('heading', { level: 2, name: 'Membros' })).toBeVisible();
    await expect(page.getByRole('cell', { name: adminEmail })).toBeVisible();
    await expectAccessible(page);
    await page.getByRole('link', { name: 'Convidar pessoa' }).click();
    await expect(page.getByRole('heading', { level: 2, name: 'Convidar pessoa' })).toBeVisible();
    await page.getByLabel('E-mail').fill(lawyerEmail);
    await page.getByRole('combobox', { name: 'Perfil' }).click();
    await page.getByRole('option', { name: 'Advogado' }).click();
    await expectAccessible(page);
    await page.getByRole('button', { name: 'Enviar convite' }).click();
    await expect(page.getByRole('status')).toContainText(lawyerEmail);
    await page.getByRole('button', { name: 'Sair' }).click();
    await expect(page).toHaveURL(/\/entrar$/);

    // A advogada abre o link do convite enviado pelo Worker e cria a conta com o e-mail já preenchido.
    await page.goto(await linkFromEmail(request, mailpitUrl!, lawyerEmail, '/convite'));
    await expect(
      page.getByRole('heading', { level: 1, name: `Convite para ${office}` }),
    ).toBeVisible();
    await expectAccessible(page);
    await page.getByRole('link', { name: 'Criar conta' }).click();
    await expect(page.getByLabel('E-mail')).toHaveValue(lawyerEmail);
    await registerAndConfirm(page, request, 'Beatriz Advogada', lawyerEmail);

    // Login: o SPA volta ao convite guardado na aba, e o aceite a leva ao escritório.
    await page.goto('/');
    await page.getByRole('button', { name: 'Entrar' }).click();
    await submitSignIn(page, lawyerEmail);
    await expect(
      page.getByRole('heading', { level: 1, name: `Convite para ${office}` }),
    ).toBeVisible();
    await expect(page.getByText(`Você entrou como ${lawyerEmail}.`)).toBeVisible();
    await page.getByRole('button', { name: 'Aceitar convite' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Início' })).toBeVisible();
    await expect(page.getByTestId('active-organization')).toHaveText(office);
    await expect(page.getByRole('link', { name: 'Configurações' })).toHaveCount(0);
  });
});
