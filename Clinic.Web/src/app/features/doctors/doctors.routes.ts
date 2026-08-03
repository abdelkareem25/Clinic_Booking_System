import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';

export const DOCTORS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard('doctors.view')],
    title: 'Doctors | Clinic',
    loadComponent: () =>
      import('./doctor-list/doctor-list.component').then((m) => m.DoctorListComponent),
  },
];
