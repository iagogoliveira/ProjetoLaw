import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

/** Funil comercial (RF-08, `features/funnel`): kanban, oportunidades e propostas. */
export const FUNNEL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/funnel-page').then((m) => m.FunnelPage),
  },
  {
    path: 'nova',
    canMatch: [permissionGuard('crm.opportunities.manage')],
    loadComponent: () => import('./pages/opportunity-form-page').then((m) => m.OpportunityFormPage),
  },
  {
    path: 'propostas/:proposalId',
    canMatch: [permissionGuard('crm.proposals.read')],
    loadComponent: () => import('./pages/proposal-page').then((m) => m.ProposalPage),
  },
  {
    path: ':id/editar',
    canMatch: [permissionGuard('crm.opportunities.manage')],
    loadComponent: () => import('./pages/opportunity-form-page').then((m) => m.OpportunityFormPage),
  },
  {
    path: ':id/propostas/nova',
    canMatch: [permissionGuard('crm.proposals.manage')],
    loadComponent: () => import('./pages/proposal-page').then((m) => m.ProposalPage),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/opportunity-detail-page').then((m) => m.OpportunityDetailPage),
  },
];
