import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';

import { Pagination } from '../models/pagination.model';
import { AdminStatistics, DashboardData, DoctorStatistics } from '../models/statistics.model';
import { deriveAppointmentStatus } from '../utils/appointment-status.util';
import { AppointmentsService } from './appointments.service';
import { DoctorsService } from './doctors.service';
import { PatientsService } from './patients.service';
import { SchedulesService } from './schedules.service';

const EMPTY_PAGE: Pagination<never> = { pageIndex: 1, pageSize: 0, count: 0, data: [] };

@Injectable({ providedIn: 'root' })
export class StatisticsService {
  private readonly appointments = inject(AppointmentsService);
  private readonly doctors = inject(DoctorsService);
  private readonly patients = inject(PatientsService);
  private readonly schedules = inject(SchedulesService);

  /**
   * Aggregates the dashboard figures. Each source call is made resilient with
   * `catchError` because the backend returns 404 (not an empty page) when a
   * collection has no rows.
   */
  getDashboardData(): Observable<DashboardData> {
    return forkJoin({
      doctors: this.safe(this.doctors.getDoctors({ pageIndex: 1, pageSize: 1 })),
      patients: this.safe(this.patients.getPatients({ pageIndex: 1, pageSize: 1 })),
      appointments: this.safe(
        this.appointments.getAppointments({ pageIndex: 1, pageSize: 20, sort: 'Descending' })
      )
    }).pipe(
      map(({ doctors, patients, appointments }) => {
        const now = new Date();
        return {
          totalDoctors: doctors.count,
          totalPatients: patients.count,
          totalAppointments: appointments.count,
          todaysAppointments: appointments.data.filter(
            (item) => deriveAppointmentStatus(item.appointmentDate, now) === 'Today'
          ).length,
          recentAppointments: [...appointments.data]
            .sort((a, b) => Date.parse(b.appointmentDate) - Date.parse(a.appointmentDate))
            .slice(0, 5)
        };
      })
    );
  }

  getAdminStatistics(): Observable<AdminStatistics> {
    return forkJoin({
      doctors: this.safe(this.doctors.getDoctors({ pageIndex: 1, pageSize: 1 })),
      patients: this.safe(this.patients.getPatients({ pageIndex: 1, pageSize: 1 })),
      appointments: this.safe(this.appointments.getAppointments({ pageIndex: 1, pageSize: 1 })),
      schedules: this.safe(this.schedules.getSchedules({ pageIndex: 1, pageSize: 1 }))
    }).pipe(
      map(({ doctors, patients, appointments, schedules }) => ({
        doctors: doctors.count,
        patients: patients.count,
        appointments: appointments.count,
        schedules: schedules.count
      }))
    );
  }

  getDoctorStatistics(doctorId: number): Observable<DoctorStatistics> {
    return this.safe(this.appointments.getAppointments({ doctorId, pageIndex: 1, pageSize: 20 })).pipe(
      map((page) => {
        const now = new Date();
        return {
          totalAppointments: page.count,
          upcomingAppointments: page.data.filter(
            (appointment) => deriveAppointmentStatus(appointment.appointmentDate, now) !== 'Past'
          ).length,
          completedAppointments: page.data.filter(
            (appointment) => deriveAppointmentStatus(appointment.appointmentDate, now) === 'Past'
          ).length
        };
      })
    );
  }

  private safe<T>(source: Observable<Pagination<T>>): Observable<Pagination<T>> {
    return source.pipe(catchError(() => of(EMPTY_PAGE as Pagination<T>)));
  }
}
