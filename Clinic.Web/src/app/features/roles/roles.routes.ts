import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';

export const ROLES_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard('roles.view')],
    title: 'Roles & permissions | Clinic',
    loadComponent: () =>
      import('./role-list/role-list.component').then((m) => m.RoleListComponent),
  },
];
