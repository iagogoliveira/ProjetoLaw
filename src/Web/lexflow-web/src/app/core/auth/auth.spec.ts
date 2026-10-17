import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { oidcConfiguration } from './auth-config';
import { anonymousGuard, authGuard } from './auth.guard';
import { AuthSession } from './auth-session';

describe('oidcConfiguration', () => {
  it('uses authorization code with PKCE, rotating refresh tokens and the API as the only secure route', () => {
    const config = oidcConfiguration(
      { apiUrl: 'http://api.test', authority: 'http://id.test' },
      'http://app.test',
    );

    expect(config).toMatchObject({
      authority: 'http://id.test',
      clientId: 'lexflow-web',
      responseType: 'code',
      redirectUrl: 'http://app.test/auth/callback',
      postLogoutRedirectUri: 'http://app.test/',
      useRefreshToken: true,
      useRefreshTokenLock: true,
      secureRoutes: ['http://api.test'],
      autoUserInfo: false,
    });
    expect(config.scope?.split(' ')).toEqual([
      'openid',
      'profile',
      'email',
      'offline_access',
      'api',
    ]);
  });
});

describe('guards', () => {
  function setup(authenticated: boolean) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthSession,
          useValue: { initialize: () => Promise.resolve(), isAuthenticated: signal(authenticated) },
        },
      ],
    });
    return TestBed.inject(Router);
  }

  const run = (guard: typeof authGuard) =>
    TestBed.runInInjectionContext(() =>
      guard({ path: '' }, [], {} as Parameters<typeof authGuard>[2]),
    ) as Promise<boolean | UrlTree>;

  it('lets signed-in users into internal routes', async () => {
    setup(true);

    expect(await run(authGuard)).toBe(true);
  });

  it('sends anonymous users to the sign-in page', async () => {
    const router = setup(false);

    expect(router.serializeUrl((await run(authGuard)) as UrlTree)).toBe('/entrar');
  });

  it('keeps signed-in users away from the sign-in page', async () => {
    const router = setup(true);

    expect(router.serializeUrl((await run(anonymousGuard)) as UrlTree)).toBe('/');
  });
});
