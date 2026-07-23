import { Routes } from '@angular/router';

import { PatientDetailComponent } from './patient-detail/patient-detail.component';
import { PatientListComponent } from './patient-list/patient-list.component';

export const PATIENTS_ROUTES: Routes = [
  { path: '', component: PatientListComponent, title: 'Patients | Clinic Booking' },
  {
    path: ':id',
    component: PatientDetailComponent,
    title: 'Patient Details | Clinic Booking',
    data: { breadcrumb: 'Details' }
  }
];
