import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';

import { Appointment } from '../../core/models/appointment.model';
import { Doctor } from '../../core/models/doctor.model';
import { Pagination } from '../../core/models/pagination.model';
import { DoctorSchedule } from '../../core/models/schedule.model';
import { AppointmentsService } from '../../core/services/appointments.service';
import { DoctorsService } from '../../core/services/doctors.service';
import { PatientsService } from '../../core/services/patients.service';
import { SchedulesService } from '../../core/services/schedules.service';

export interface DashboardSnapshot {
  totalPatients: number;
  totalDoctors: number;
  doctors: Doctor[];
  schedules: DoctorSchedule[];
  appointments: Appointment[];
  totalAppointments: number;
}

const EMPTY_PAGE = <T,>(): Pagination<T> => ({ pageIndex: 1, pageSize: 0, count: 0, data: [] });

/**
 * Loads everything the dashboard needs in one round trip.
 *
 * The API has no aggregate endpoint, so the figures are composed here from the
 * list endpoints rather than in the component — which keeps the component about
 * presentation and means a future `/dashboard` endpoint replaces this one file.
 *
 * Every call is wrapped in `catchError`: these endpoints answer **404 when a
 * collection is empty**, and a brand-new clinic would otherwise see an error
 * screen instead of an empty dashboard.
 */
@Injectable({ providedIn: 'root' })
export class DashboardData {
  private readonly appointments = inject(AppointmentsService);
  private readonly doctors = inject(DoctorsService);
  private readonly patients = inject(PatientsService);
  private readonly schedules = inject(SchedulesService);

  load(): Observable<DashboardSnapshot> {
    return forkJoin({
      patients: this.safe(this.patients.getPatients({ pageIndex: 1, pageSize: 1 })),
      doctors: this.safe(this.doctors.getDoctors({ pageIndex: 1, pageSize: 100 })),
      schedules: this.safe(this.schedules.getSchedules({ pageIndex: 1, pageSize: 200 })),
      // A wide page rather than several filtered calls: the endpoint cannot
      // filter by date range, so the slicing happens client-side anyway.
      appointments: this.safe(
        this.appointments.getAppointments({ pageIndex: 1, pageSize: 200, sort: 'Descending' })
      ),
    }).pipe(
      map(({ patients, doctors, schedules, appointments }) => ({
        totalPatients: patients.count,
        totalDoctors: doctors.count,
        doctors: doctors.data,
        schedules: schedules.data,
        appointments: appointments.data,
        totalAppointments: appointments.count,
      }))
    );
  }

  private safe<T>(source: Observable<Pagination<T>>): Observable<Pagination<T>> {
    return source.pipe(catchError(() => of(EMPTY_PAGE<T>())));
  }
}
