import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';

export const USERS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard('users.view')],
    title: 'Users | Clinic',
    loadComponent: () =>
      import('./user-list/user-list.component').then((m) => m.UserListComponent),
  },
];
