import { Routes } from '@angular/router';
import { anonymousGuard, authGuard } from './core/auth/auth.guard';
import { organizationGuard } from './core/organization/organization.guards';

export const routes: Routes = [
  {
    path: 'entrar',
    canMatch: [anonymousGuard],
    loadComponent: () => import('./core/auth/pages/sign-in-page').then((m) => m.SignInPage),
  },
  {
    path: 'auth/callback',
    loadComponent: () =>
      import('./core/auth/pages/auth-callback-page').then((m) => m.AuthCallbackPage),
  },
  {
    // Link do convite: público (a página oferece entrar ou criar conta e volta depois do login).
    path: 'convite',
    loadComponent: () =>
      import('./features/invitation/pages/invitation-page').then((m) => m.InvitationPage),
  },
  {
    path: '',
    canMatch: [authGuard],
    loadComponent: () => import('./core/layout/shell/shell').then((m) => m.Shell),
    children: [
      {
        path: 'escritorio/novo',
        loadComponent: () =>
          import('./features/organization/pages/create-organization-page').then(
            (m) => m.CreateOrganizationPage,
          ),
      },
      {
        path: 'configuracoes',
        canMatch: [organizationGuard],
        loadChildren: () =>
          import('./features/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
      },
      {
        path: 'conta',
        loadChildren: () =>
          import('./features/account/account.routes').then((m) => m.ACCOUNT_ROUTES),
      },
      // Por último: a rota vazia casa como prefixo de qualquer caminho, e o guard dela redireciona quem não tem
      // escritório para o onboarding (antes das outras, viraria um laço de redirecionamento).
      {
        path: '',
        canMatch: [organizationGuard],
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
