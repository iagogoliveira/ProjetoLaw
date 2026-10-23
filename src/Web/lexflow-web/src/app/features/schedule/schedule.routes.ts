import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/organization/organization.guards';

export const SCHEDULE_ROUTES: Routes = [
  {
    path: 'sincronizacao',
    canMatch: [permissionGuard('identity.profile.read')],
    loadComponent: () =>
      import('../calendar-sync/calendar-sync-page').then((m) => m.CalendarSyncPage),
  },
  {
    path: '',
    canMatch: [permissionGuard('schedule.events.read')],
    loadComponent: () => import('./pages/agenda-page').then((m) => m.AgendaPage),
  },
  {
    path: 'compromissos/novo',
    canMatch: [permissionGuard('schedule.events.manage')],
    loadComponent: () => import('./events/event-editor-page').then((m) => m.EventEditorPage),
  },
  {
    path: 'compromissos/:id',
    canMatch: [permissionGuard('schedule.events.read')],
    loadComponent: () => import('./events/event-editor-page').then((m) => m.EventEditorPage),
  },
];
