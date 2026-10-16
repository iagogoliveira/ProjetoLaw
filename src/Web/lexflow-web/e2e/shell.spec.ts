import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test.describe('shell', () => {
  test('abre layout vazio em pt-BR sem violações de acessibilidade', async ({ page }) => {
    await page.goto('/');

    await expect(page.locator('html')).toHaveAttribute('lang', 'pt-BR');
    await expect(page).toHaveTitle('LexFlow');
    await expect(page.getByTestId('brand')).toHaveText('LexFlow');
    await expect(page.getByRole('navigation', { name: 'Navegação principal' })).toBeAttached();
    await expect(page.getByRole('heading', { level: 1, name: 'Painel' })).toBeVisible();
    await expect(page.getByTestId('empty-state')).toHaveText('Ainda não há nada por aqui.');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();
    expect(results.violations).toEqual([]);
  });
});
