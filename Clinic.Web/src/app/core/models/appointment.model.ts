import { PageQuery } from './pagination.model';

/** The stored lifecycle of a booking. Mirrors the API's `AppointmentStatus` enum. */
export type AppointmentStatus = 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled';

/**
 * A second, derived dimension: where the appointment sits relative to now.
 *
 * Kept alongside the stored status rather than replaced by it. The two answer
 * different questions — "has this been confirmed" and "is it today" — and the
 * front desk filters by both.
 */
export type AppointmentTimeStatus = 'Upcoming' | 'Today' | 'Past';

export const APPOINTMENT_TIME_STATUSES: AppointmentTimeStatus[] = ['Upcoming', 'Today', 'Past'];

export interface Appointment {
  id: number;
  /** Always present now that the list specification eager-loads the navigations. */
  doctorId: number;
  doctorName: string;
  patientId: number;
  patientName: string;
  appointmentDate: string;
  startTime: string;
  endTime: string;
  status: AppointmentStatus;
  notes?: string | null;
}

export interface CreateAppointmentRequest {
  patientId: number;
  doctorId: number;
  appointmentDate: string;
  notes?: string | null;
}

export interface UpdateAppointmentRequest {
  patientId: number;
  doctorId: number;
  appointmentDate: string;
  /** Omitted leaves the stored status untouched — see the API's `UpdateAppointmentDto`. */
  status?: AppointmentStatus;
  notes?: string | null;
}

export interface AppointmentQuery extends PageQuery {
  doctorId?: number;
  patientId?: number;
  status?: AppointmentStatus;
}

export const APPOINTMENT_STATUSES: AppointmentStatus[] = [
  'Pending',
  'Confirmed',
  'Completed',
  'Cancelled'
];
