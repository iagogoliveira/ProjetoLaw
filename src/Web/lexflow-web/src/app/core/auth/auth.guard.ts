import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { SIGN_IN_PATH } from './auth-config';
import { AuthSession } from './auth-session';

// inject() só funciona antes do primeiro await (contexto de injeção síncrono).

/** Rotas internas exigem login; sem ele, vai para `/entrar`. A UI só esconde: quem bloqueia é o servidor. */
export const authGuard: CanMatchFn = async () => {
  const session = inject(AuthSession);
  const router = inject(Router);
  await session.initialize();
  return session.isAuthenticated() ? true : router.createUrlTree([SIGN_IN_PATH]);
};

/** Quem já entrou não vê a página de login. */
export const anonymousGuard: CanMatchFn = async () => {
  const session = inject(AuthSession);
  const router = inject(Router);
  await session.initialize();
  return session.isAuthenticated() ? router.createUrlTree(['/']) : true;
};
