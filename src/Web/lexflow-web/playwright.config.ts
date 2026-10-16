import { defineConfig, devices } from '@playwright/test';

// E2E_BASE_URL aponta para a Web já em execução (ex.: AppHost no CI);
// sem ela, o Playwright sobe o `ng serve` localmente.
const externalBaseUrl = process.env['E2E_BASE_URL'];
const baseURL = externalBaseUrl ?? 'http://localhost:4200';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL,
    locale: 'pt-BR',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: externalBaseUrl
    ? undefined
    : {
        command: 'npm run start',
        url: baseURL,
        reuseExistingServer: !process.env['CI'],
        timeout: 180_000,
      },
});
