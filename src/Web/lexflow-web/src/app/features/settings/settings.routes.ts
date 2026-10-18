import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

/** Configurações do escritório (seção 5.1, `features/settings`). Cada tela exige a permissão de leitura dela. */
export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/settings-layout').then((m) => m.SettingsLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'usuarios' },
      {
        path: 'escritorio',
        canMatch: [permissionGuard('settings.organization.manage')],
        loadComponent: () =>
          import('./pages/organization-settings-page').then((m) => m.OrganizationSettingsPage),
      },
      {
        path: 'usuarios',
        canMatch: [permissionGuard('settings.users.read')],
        loadComponent: () => import('./pages/users-page').then((m) => m.UsersPage),
      },
      {
        path: 'convites/novo',
        canMatch: [permissionGuard('settings.users.manage')],
        loadComponent: () => import('./pages/invite-page').then((m) => m.InvitePage),
      },
      {
        path: 'perfis',
        canMatch: [permissionGuard('settings.users.read')],
        loadComponent: () => import('./pages/roles-page').then((m) => m.RolesPage),
      },
      {
        path: 'estrutura',
        canMatch: [permissionGuard('settings.structure.manage')],
        loadComponent: () => import('./pages/structure-page').then((m) => m.StructurePage),
      },
      {
        // A tela é do funil (features/funnel); aqui só a aba nas configurações.
        path: 'modelos-proposta',
        canMatch: [permissionGuard('crm.proposals.read')],
        loadComponent: () =>
          import('../funnel/pages/proposal-templates-page').then((m) => m.ProposalTemplatesPage),
      },
      {
        path: 'campos',
        canMatch: [permissionGuard('settings.custom_fields.read')],
        loadComponent: () => import('./pages/custom-fields-page').then((m) => m.CustomFieldsPage),
      },
    ],
  },
];
