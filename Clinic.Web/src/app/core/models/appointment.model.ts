import { PageQuery } from './pagination.model';

export type AppointmentStatus = 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled';

/**
 * The backend `AppointmentDto` carries no status field, so we derive a
 * time-based status client-side from the appointment date.
 */
export type AppointmentTimeStatus = 'Upcoming' | 'Today' | 'Past';

export const APPOINTMENT_TIME_STATUSES: AppointmentTimeStatus[] = ['Upcoming', 'Today', 'Past'];

export interface Appointment {
  id: number;
  doctorName: string;
  patientName: string;
  appointmentDate: string;
  doctorId?: number;
  patientId?: number;
  status?: AppointmentStatus | string;
}

export interface CreateAppointmentRequest {
  patientId: number;
  doctorId: number;
  appointmentDate: string;
}

export interface UpdateAppointmentRequest {
  patientId: number;
  doctorId: number;
  appointmentDate: string;
}

export interface AppointmentQuery extends PageQuery {
  doctorId?: number;
  patientId?: number;
  status?: string;
}

export const APPOINTMENT_STATUSES: AppointmentStatus[] = [
  'Pending',
  'Confirmed',
  'Completed',
  'Cancelled'
];

