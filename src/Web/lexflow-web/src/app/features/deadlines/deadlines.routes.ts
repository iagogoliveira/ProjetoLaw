import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

export const DEADLINES_ROUTES: Routes = [
  {
    path: '',
    canMatch: [permissionGuard('schedule.deadlines.read')],
    loadComponent: () => import('./pages/deadlines-list-page').then((m) => m.DeadlinesListPage),
  },
  {
    path: 'novo',
    canMatch: [permissionGuard('schedule.deadlines.manage')],
    loadComponent: () => import('./pages/deadline-create-page').then((m) => m.DeadlineCreatePage),
  },
  {
    path: ':id',
    canMatch: [permissionGuard('schedule.deadlines.read')],
    loadComponent: () => import('./pages/deadline-detail-page').then((m) => m.DeadlineDetailPage),
  },
];
