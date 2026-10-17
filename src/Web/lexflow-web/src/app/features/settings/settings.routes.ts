import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

/** Configurações do escritório (seção 5.1, `features/settings`); as telas completas entram no PR 3F2. */
export const SETTINGS_ROUTES: Routes = [
  {
    path: 'convites/novo',
    canMatch: [permissionGuard('settings.users.manage')],
    loadComponent: () => import('./pages/invite-page').then((m) => m.InvitePage),
  },
];
