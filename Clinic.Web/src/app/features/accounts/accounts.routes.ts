import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';

export const ACCOUNTS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard('accounts.view')],
    title: 'Accounts | Clinic',
    loadComponent: () => import('./accounts.component').then((m) => m.AccountsComponent),
  },
];
