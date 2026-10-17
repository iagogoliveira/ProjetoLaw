import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import localePt from '@angular/common/locales/pt';
import {
  ApplicationConfig,
  inject,
  LOCALE_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import {
  AbstractSecurityStorage,
  authInterceptor,
  DefaultSessionStorageService,
  provideAuth,
} from 'angular-auth-oidc-client';
import { client } from './api/client.gen';
import { provideHeyApiClient } from './api/client/client.gen';
import { routes } from './app.routes';
import { oidcConfiguration } from './core/auth/auth-config';
import { AuthSession } from './core/auth/auth-session';
import { APP_CONFIG, AppConfig } from './core/config/app-config';
import { organizationInterceptor } from './core/organization/active-organization';
import { DEFAULT_LANG, provideI18n } from './core/i18n/transloco.config';

registerLocaleData(localePt, DEFAULT_LANG);

export function createAppConfig(config: AppConfig): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      provideZonelessChangeDetection(),
      provideRouter(routes, withComponentInputBinding()),
      { provide: LOCALE_ID, useValue: DEFAULT_LANG },
      { provide: APP_CONFIG, useValue: config },
      provideI18n(),
      provideHttpClient(withInterceptors([authInterceptor(), organizationInterceptor])),
      provideAuth({ config: oidcConfiguration(config, window.location.origin) }),
      // Tokens na sessão da aba (decisão de 2026-10-16): sobrevivem ao F5 e somem ao fechar a aba; nunca em localStorage.
      { provide: AbstractSecurityStorage, useClass: DefaultSessionStorageService },
      provideHeyApiClient(client),
      provideAppInitializer(() => {
        client.setConfig({ baseUrl: config.apiUrl });
        return inject(AuthSession).initialize();
      }),
    ],
  };
}
