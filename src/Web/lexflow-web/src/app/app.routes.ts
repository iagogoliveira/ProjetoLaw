import { Routes } from '@angular/router';
import { anonymousGuard, authGuard } from './core/auth/auth.guard';

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
    path: '',
    canMatch: [authGuard],
    loadComponent: () => import('./core/layout/shell/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'conta',
        loadChildren: () =>
          import('./features/account/account.routes').then((m) => m.ACCOUNT_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
