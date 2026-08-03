import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';

export const PATIENTS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard('patients.view')],
    title: 'Patients | Clinic',
    loadComponent: () =>
      import('./patient-list/patient-list.component').then((m) => m.PatientListComponent),
  },
  {
    // Declared before `:id` so "new" is never parsed as a patient id.
    path: 'new',
    canActivate: [permissionGuard('patients.create')],
    title: 'New patient | Clinic',
    data: { breadcrumb: 'patients.new' },
    loadComponent: () =>
      import('./patient-form/patient-form.component').then((m) => m.PatientFormComponent),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard('patients.edit')],
    title: 'Edit patient | Clinic',
    data: { breadcrumb: 'common.edit' },
    loadComponent: () =>
      import('./patient-form/patient-form.component').then((m) => m.PatientFormComponent),
  },
  {
    path: ':id',
    canActivate: [permissionGuard('patients.view')],
    title: 'Patient | Clinic',
    data: { breadcrumb: 'common.details' },
    loadComponent: () =>
      import('./patient-detail/patient-detail.component').then((m) => m.PatientDetailComponent),
  },
];
