import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  Appointment,
  AppointmentQuery,
  AppointmentStatus,
  CreateAppointmentRequest,
  UpdateAppointmentRequest
} from '../models/appointment.model';
import { Pagination } from '../models/pagination.model';
import { toUpdateRequest } from '../utils/appointment-time.util';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class AppointmentsService {
  private readonly api = inject(ApiService);

  getAppointments(query: AppointmentQuery = {}): Observable<Pagination<Appointment>> {
    return this.api.get<Pagination<Appointment>>('Appointments', {
      PageIndex: query.pageIndex,
      PageSize: query.pageSize,
      DoctorId: query.doctorId,
      PatientId: query.patientId,
      Status: query.status,
      From: query.from,
      To: query.to,
      Sort: query.sort
    });
  }

  getAppointment(id: number): Observable<Appointment> {
    return this.api.get<Appointment>(`Appointments/${id}`);
  }

  getByDoctorName(doctorName: string): Observable<Appointment[]> {
    return this.api.get<Appointment[]>(`Appointments/doctor/${encodeURIComponent(doctorName)}`);
  }

  getByPatientName(patientName: string): Observable<Appointment[]> {
    return this.api.get<Appointment[]>(`Appointments/patient/${encodeURIComponent(patientName)}`);
  }

  createAppointment(payload: CreateAppointmentRequest): Observable<Appointment> {
    return this.api.post<Appointment>('Appointments', payload);
  }

  updateAppointment(id: number, payload: UpdateAppointmentRequest): Observable<Appointment> {
    return this.api.put<Appointment>(`Appointments/${id}`, payload);
  }

  /**
   * Moves an appointment along its lifecycle.
   *
   * Goes through `PUT /Appointments/{id}`, not a status-only endpoint: the API
   * has no `PATCH /Appointments/{id}/status`, which is what this used to call —
   * it would have returned 405 the first time anything invoked it. Status is a
   * member of `UpdateAppointmentDto`, so the update endpoint is the supported
   * route, and it needs the whole record; see `toUpdateRequest`.
   */
  changeStatus(
    appointment: Appointment,
    status: AppointmentStatus,
    fallbackDurationMinutes: number
  ): Observable<Appointment> {
    return this.updateAppointment(appointment.id, {
      ...toUpdateRequest(appointment, fallbackDurationMinutes),
      status
    });
  }

  /**
   * Moves an appointment to a new start time and, optionally, a new doctor.
   *
   * The server re-runs both booking rules (inside the doctor's published hours,
   * no overlap) and answers 400 or 409 when the move is not allowed, so a drag
   * on the calendar cannot write a slot the booking form would have refused.
   */
  reschedule(
    appointment: Appointment,
    startsAt: string,
    fallbackDurationMinutes: number,
    doctorId: number = appointment.doctorId
  ): Observable<Appointment> {
    return this.updateAppointment(appointment.id, {
      ...toUpdateRequest(appointment, fallbackDurationMinutes),
      appointmentDate: startsAt,
      doctorId
    });
  }

  cancelAppointment(id: number): Observable<void> {
    return this.api.delete<void>(`Appointments/${id}`);
  }
}

