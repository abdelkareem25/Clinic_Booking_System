import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';

export const APPOINTMENTS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard('appointments.view')],
    title: 'Appointments | Clinic',
    loadComponent: () =>
      import('./appointment-list/appointment-list.component').then(
        (m) => m.AppointmentListComponent
      ),
  },
  {
    // Before `:id` so "new" is never parsed as an appointment id.
    path: 'new',
    canActivate: [permissionGuard('appointments.create')],
    title: 'Book appointment | Clinic',
    data: { breadcrumb: 'appointments.new' },
    loadComponent: () =>
      import('./appointment-form/appointment-form.component').then(
        (m) => m.AppointmentFormComponent
      ),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard('appointments.edit')],
    title: 'Reschedule | Clinic',
    data: { breadcrumb: 'common.edit' },
    loadComponent: () =>
      import('./appointment-form/appointment-form.component').then(
        (m) => m.AppointmentFormComponent
      ),
  },
  {
    path: ':id',
    canActivate: [permissionGuard('appointments.view')],
    title: 'Appointment | Clinic',
    data: { breadcrumb: 'common.details' },
    loadComponent: () =>
      import('./appointment-detail/appointment-detail.component').then(
        (m) => m.AppointmentDetailComponent
      ),
  },
];
