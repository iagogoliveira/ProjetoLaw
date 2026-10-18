import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

/** Pessoas (RF-06, `features/people`): lista e busca, cadastro, edição e detalhe com abas. */
export const PEOPLE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/people-list-page').then((m) => m.PeopleListPage),
  },
  {
    path: 'nova',
    canMatch: [permissionGuard('crm.people.create')],
    loadComponent: () => import('./pages/person-form-page').then((m) => m.PersonFormPage),
  },
  {
    path: ':id/editar',
    canMatch: [permissionGuard('crm.people.edit')],
    loadComponent: () => import('./pages/person-form-page').then((m) => m.PersonFormPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/person-detail-page').then((m) => m.PersonDetailPage),
  },
];

/** Conflito de interesses (RF-07): verificação e relatório. */
export const CONFLICT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/conflict-check-page').then((m) => m.ConflictCheckPage),
  },
  {
    path: 'relatorio',
    loadComponent: () => import('./pages/conflict-report-page').then((m) => m.ConflictReportPage),
  },
];
