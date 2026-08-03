import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';

export const RECORDS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard('records.view')],
    title: 'Medical records | Clinic',
    loadComponent: () =>
      import('./record-list/record-list.component').then((m) => m.RecordListComponent),
  },
];
