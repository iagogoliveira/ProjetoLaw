import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

export const TASKS_ROUTES: Routes = [
  {
    path: '',
    canMatch: [permissionGuard('schedule.tasks.read')],
    loadComponent: () => import('./pages/tasks-list-page').then((m) => m.TasksListPage),
  },
  {
    path: 'novo',
    canMatch: [permissionGuard('schedule.tasks.manage')],
    loadComponent: () => import('./pages/task-editor-page').then((m) => m.TaskEditorPage),
  },
  {
    path: ':id/editar',
    canMatch: [permissionGuard('schedule.tasks.manage')],
    loadComponent: () => import('./pages/task-editor-page').then((m) => m.TaskEditorPage),
  },
  {
    path: ':id',
    canMatch: [permissionGuard('schedule.tasks.read')],
    loadComponent: () => import('./pages/task-detail-page').then((m) => m.TaskDetailPage),
  },
];
