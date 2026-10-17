import { LogLevel, OpenIdConfiguration } from 'angular-auth-oidc-client';
import { AppConfig } from '../config/app-config';

export const CLIENT_ID = 'lexflow-web';
export const CALLBACK_PATH = '/auth/callback';
export const SIGN_IN_PATH = '/entrar';

/**
 * OIDC contra o OpenIddict da API (seção 5.3): Authorization Code + PKCE, refresh token rotativo (sem iframe) e
 * token anexado só às chamadas para a API. O servidor não tolera reuso de refresh token (ADR 0014), por isso a
 * renovação é serializada entre abas (`useRefreshTokenLock`).
 */
export function oidcConfiguration(config: AppConfig, origin: string): OpenIdConfiguration {
  return {
    authority: config.authority,
    clientId: CLIENT_ID,
    redirectUrl: `${origin}${CALLBACK_PATH}`,
    postLogoutRedirectUri: `${origin}/`,
    scope: 'openid profile email offline_access api',
    responseType: 'code',
    silentRenew: true,
    useRefreshToken: true,
    useRefreshTokenLock: true,
    ignoreNonceAfterRefresh: true,
    // Nome e e-mail vêm no id token (escopos profile e email); o servidor não expõe userinfo.
    autoUserInfo: false,
    renewTimeBeforeTokenExpiresInSeconds: 30,
    secureRoutes: [config.apiUrl],
    unauthorizedRoute: SIGN_IN_PATH,
    triggerAuthorizationResultEvent: true,
    logLevel: LogLevel.Warn,
  };
}
