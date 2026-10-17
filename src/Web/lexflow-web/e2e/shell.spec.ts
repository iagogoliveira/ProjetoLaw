import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/** Sem login, o SPA mostra só a página pública `/entrar` (o shell exige autenticação). */
test.describe('Sign-in page', () => {
  test('anonymous users land on the sign-in page in pt-BR', async ({ page }) => {
    await page.goto('/');

    await expect(page).toHaveURL(/\/entrar$/);
    await expect(page).toHaveTitle('LexFlow');
    await expect(page.locator('html')).toHaveAttribute('lang', 'pt-BR');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Bem-vindo ao LexFlow' }),
    ).toBeVisible();
    await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Criar conta' })).toHaveAttribute(
      'href',
      /\/conta\/cadastro$/,
    );
  });

  test('internal routes require sign-in', async ({ page }) => {
    await page.goto('/conta');

    await expect(page).toHaveURL(/\/entrar$/);
  });

  test('has no WCAG 2.1 AA accessibility violations', async ({ page }) => {
    await page.goto('/entrar');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });

  test('has no accessibility violations in dark mode', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/entrar');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
