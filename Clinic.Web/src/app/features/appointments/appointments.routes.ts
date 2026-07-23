import { Routes } from '@angular/router';

import { AppointmentDetailComponent } from './appointment-detail/appointment-detail.component';
import { AppointmentListComponent } from './appointment-list/appointment-list.component';

export const APPOINTMENTS_ROUTES: Routes = [
  { path: '', component: AppointmentListComponent, title: 'Appointments | Clinic Booking' },
  {
    path: ':id',
    component: AppointmentDetailComponent,
    title: 'Appointment Details | Clinic Booking',
    data: { breadcrumb: 'Details' }
  }
];
