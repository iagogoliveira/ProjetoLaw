import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { AccessStore } from './access-store';

export const CREATE_ORGANIZATION_PATH = '/escritorio/novo';

// inject() só funciona antes do primeiro await (contexto de injeção síncrono).

/** Telas de escritório exigem um escritório: quem ainda não tem vai para o onboarding. */
export const organizationGuard: CanMatchFn = async () => {
  const access = inject(AccessStore);
  const router = inject(Router);
  await access.ensureLoaded();
  return !access.loaded() || access.organizations().length > 0
    ? true
    : router.createUrlTree([CREATE_ORGANIZATION_PATH]);
};

/** Rota só para quem tem a permissão na organização ativa; os outros voltam ao início. */
export function permissionGuard(permission: string): CanMatchFn {
  return async () => {
    const access = inject(AccessStore);
    const router = inject(Router);
    await access.ensureLoaded();
    return access.can(permission) ? true : router.createUrlTree(['/']);
  };
}
