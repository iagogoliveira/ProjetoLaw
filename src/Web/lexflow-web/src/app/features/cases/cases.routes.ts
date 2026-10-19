import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

/** Processos e casos (RF-09, `features/cases`): lista, assistente de cadastro, edição e detalhe com abas. */
export const CASES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/cases-list-page').then((m) => m.CasesListPage),
  },
  {
    path: 'novo',
    canMatch: [permissionGuard('cases.create')],
    loadComponent: () => import('./pages/case-wizard-page').then((m) => m.CaseWizardPage),
  },
  {
    path: ':id/editar',
    canMatch: [permissionGuard('cases.edit')],
    loadComponent: () => import('./pages/case-wizard-page').then((m) => m.CaseWizardPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/case-detail-page').then((m) => m.CaseDetailPage),
  },
];
