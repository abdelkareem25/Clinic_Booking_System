import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';

export const SCHEDULES_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard('schedules.view')],
    title: 'Schedules | Clinic',
    loadComponent: () =>
      import('./schedule-list/schedule-list.component').then((m) => m.ScheduleListComponent),
  },
];
