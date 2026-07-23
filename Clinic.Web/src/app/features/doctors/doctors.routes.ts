import { Routes } from '@angular/router';

import { DoctorDetailComponent } from './doctor-detail/doctor-detail.component';
import { DoctorListComponent } from './doctor-list/doctor-list.component';

export const DOCTORS_ROUTES: Routes = [
  { path: '', component: DoctorListComponent, title: 'Doctors | Clinic Booking' },
  {
    path: ':id',
    component: DoctorDetailComponent,
    title: 'Doctor Details | Clinic Booking',
    data: { breadcrumb: 'Details' }
  }
];
