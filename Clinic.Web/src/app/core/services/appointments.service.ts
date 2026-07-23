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

  updateStatus(id: number, status: AppointmentStatus): Observable<Appointment> {
    return this.api.patch<Appointment>(`Appointments/${id}/status`, { status });
  }

  cancelAppointment(id: number): Observable<void> {
    return this.api.delete<void>(`Appointments/${id}`);
  }
}

