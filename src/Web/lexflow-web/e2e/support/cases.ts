import { expect, Page } from '@playwright/test';

/** Campos vazios do Material são abertos pelo teclado (AGENTS.md). */
export async function choose(page: Page, label: string, option: string): Promise<void> {
  const select = page.getByRole('combobox', { name: label, exact: true });
  await expect(select).toBeEnabled();
  // O Material pode terminar de carregar opções/gerenciar foco após renderizar o controle.
  await expect(async () => {
    if ((await select.getAttribute('aria-expanded')) !== 'true') {
      await select.focus();
      await select.press('Enter');
    }
    await expect(select).toHaveAttribute('aria-expanded', 'true', { timeout: 1000 });
  }).toPass({ timeout: 10000 });
  await page.getByRole('option', { name: option, exact: true }).click();
}
export async function openCaseTab(page: Page, name: string): Promise<void> {
  const tab = page.getByRole('tab', { name, exact: true });
  await tab.focus();
  await tab.press('Enter');
  await expect(tab).toHaveAttribute('aria-selected', 'true');
}
async function next(page: Page): Promise<void> {
  const previousId = await page
    .locator('mat-stepper')
    .getByRole('button', { expanded: true })
    .getAttribute('aria-controls');
  await page.getByRole('button', { name: 'Próximo' }).filter({ visible: true }).last().click();
  await expect(page.locator(`[id="${previousId}"]`)).toBeHidden();
}
/** Caso sem CNJ criado pelo assistente real, com uma pessoa cliente e um responsável. */
export async function createSimpleCase(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Processos', exact: true }).click();
  await page.getByRole('link', { name: 'Novo processo' }).click();
  await choose(page, 'Tipo', 'Extrajudicial');
  await next(page);
  await next(page);
  await page.getByLabel('Nome ou razão social').fill('Cliente Valores');
  await page.getByRole('button', { name: 'Cadastrar como cliente' }).click();
  await expect(page.getByRole('cell', { name: 'Cliente Valores', exact: true })).toBeVisible();
  await next(page);
  const member = page.getByRole('combobox', { name: 'Membro', exact: true });
  await member.focus();
  await member.press('Enter');
  await page.getByRole('option').first().click();
  await page.getByRole('button', { name: 'Incluir', exact: true }).first().click();
  await next(page);
  await page.getByRole('button', { name: 'Cadastrar processo' }).click();
  await expect(page).toHaveURL(/\/processos\/[\da-f-]+$/);
}
