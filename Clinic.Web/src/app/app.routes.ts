import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';
import { roleGuard } from './core/guards/role.guard';
import { AuthLayoutComponent } from './layouts/auth-layout/auth-layout.component';
import { ShellLayoutComponent } from './layouts/shell-layout/shell-layout.component';

export const routes: Routes = [
  {
    path: 'auth',
    component: AuthLayoutComponent,
    canActivate: [guestGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES)
  },
  {
    path: '',
    component: ShellLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard | Clinic Booking',
        data: { breadcrumb: 'Dashboard' },
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent)
      },
      {
        path: 'doctors',
        data: { breadcrumb: 'Doctors' },
        loadChildren: () => import('./features/doctors/doctors.routes').then((m) => m.DOCTORS_ROUTES)
      },
      {
        path: 'patients',
        data: { breadcrumb: 'Patients' },
        loadChildren: () => import('./features/patients/patients.routes').then((m) => m.PATIENTS_ROUTES)
      },
      {
        path: 'appointments',
        data: { breadcrumb: 'Appointments' },
        loadChildren: () =>
          import('./features/appointments/appointments.routes').then((m) => m.APPOINTMENTS_ROUTES)
      },
      {
        path: 'doctor-schedules',
        data: { breadcrumb: 'Doctor Schedules' },
        loadChildren: () =>
          import('./features/doctor-schedules/doctor-schedules.routes').then(
            (m) => m.DOCTOR_SCHEDULES_ROUTES
          )
      },
      {
        path: 'users',
        canActivate: [roleGuard(['Admin'])],
        data: { breadcrumb: 'Users' },
        loadChildren: () => import('./features/users/users.routes').then((m) => m.USERS_ROUTES)
      },
      {
        path: 'unauthorized',
        title: 'Access denied | Clinic Booking',
        data: { breadcrumb: 'Access denied' },
        loadComponent: () =>
          import('./features/errors/unauthorized.component').then((m) => m.UnauthorizedComponent)
      },
      {
        path: '**',
        title: 'Not found | Clinic Booking',
        data: { breadcrumb: 'Not found' },
        loadComponent: () =>
          import('./features/errors/not-found.component').then((m) => m.NotFoundComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
