import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

export const PUBLICATIONS_ROUTES: Routes = [
  {
    path: 'fontes',
    canMatch: [permissionGuard('settings.integrations.manage')],
    loadComponent: () => import('./pages/sources-page').then((m) => m.SourcesPage),
  },
  {
    path: 'fechamento',
    canMatch: [permissionGuard('publications.close')],
    loadComponent: () => import('./pages/closing-page').then((m) => m.ClosingPage),
  },
  {
    path: '',
    pathMatch: 'full',
    canMatch: [permissionGuard('publications.read')],
    loadComponent: () => import('./pages/publications-page').then((m) => m.PublicationsPage),
  },
];
