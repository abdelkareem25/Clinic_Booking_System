import { Routes } from '@angular/router';

import { ScheduleListComponent } from './schedule-list/schedule-list.component';

export const DOCTOR_SCHEDULES_ROUTES: Routes = [
  { path: '', component: ScheduleListComponent, title: 'Doctor Schedules | Clinic Booking' }
];
