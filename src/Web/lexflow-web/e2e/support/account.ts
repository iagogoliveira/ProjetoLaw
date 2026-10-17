import AxeBuilder from '@axe-core/playwright';
import { APIRequestContext, expect, Page } from '@playwright/test';
import { linkFromEmail } from './mailpit';

export const mailpitUrl = process.env['E2E_MAILPIT_URL'];

export const PASSWORD = 'uma senha bem longa para o e2e';

export function newEmail(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1000)}@exemplo.com.br`;
}

export async function expectAccessible(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(results.violations).toEqual([]);
}

/** Login pelo botão "Entrar" do SPA, na página do servidor de identidade. */
export async function signInWithPassword(
  page: Page,
  email: string,
  password = PASSWORD,
): Promise<void> {
  await page.goto('/');
  await expect(page).toHaveURL(/\/entrar$/);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await submitSignIn(page, email, password);
}

/** Preenche a página de login do servidor de identidade (já aberta). */
export async function submitSignIn(page: Page, email: string, password = PASSWORD): Promise<void> {
  await expect(page).toHaveURL(/\/conta\/entrar/);
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill(password);
  await page.getByRole('button', { name: 'Entrar' }).click();
}

/** Formulário de cadastro (já aberto) e confirmação pelo link do e-mail enviado pelo Worker. */
export async function registerAndConfirm(
  page: Page,
  request: APIRequestContext,
  name: string,
  email: string,
  password = PASSWORD,
): Promise<void> {
  await page.getByLabel('Nome completo').fill(name);
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill(password);
  await page.getByRole('button', { name: 'Criar conta' }).click();
  await expect(page.getByRole('status')).toContainText('Enviamos um e-mail');

  await page.goto(await linkFromEmail(request, mailpitUrl!, email, '/conta/confirmar-email'));
  await expect(page.getByRole('status')).toContainText('E-mail confirmado');
}

/** Conta nova (cadastro, confirmação e login) que cria o próprio escritório; termina no Início, já no escritório. */
export async function createOfficeAsNewAdmin(
  page: Page,
  request: APIRequestContext,
  office: string,
  email = newEmail('admin'),
): Promise<string> {
  await page.goto('/entrar');
  await page.getByRole('link', { name: 'Criar conta' }).click();
  await registerAndConfirm(page, request, 'Ana Administradora', email);
  await signInWithPassword(page, email);
  await expect(page.getByRole('heading', { level: 1, name: 'Crie seu escritório' })).toBeVisible();
  await page.getByLabel('Nome do escritório').fill(office);
  await page.getByRole('button', { name: 'Criar escritório' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Início' })).toBeVisible();
  return email;
}
