import { InjectionToken } from '@angular/core';

/**
 * Configuração de tempo de execução (`/config.json`), trocada por ambiente no deploy sem recompilar o SPA.
 * `authority` é o servidor de identidade (OpenIddict); `apiUrl`, a base da API REST.
 */
export interface AppConfig {
  readonly apiUrl: string;
  readonly authority: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
