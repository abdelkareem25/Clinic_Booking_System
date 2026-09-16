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
  /**
   * Length of the booking. The API defaults it to 30 when absent, which silently
   * disagreed with a clinic that had set a different slot length: the picker
   * offered 45-minute slots and the server booked 30, so the diary and the
   * database described different appointments.
   */
  durationMinutes?: number;
  notes?: string | null;
}

export interface UpdateAppointmentRequest {
  patientId: number;
  doctorId: number;
  appointmentDate: string;
  /**
   * Required in practice, even though the API treats it as optional: it defaults
   * to 30, so omitting it while rescheduling would shorten a 60-minute
   * appointment to half its length.
   */
  durationMinutes?: number;
  /** Omitted leaves the stored status untouched — see the API's `UpdateAppointmentDto`. */
  status?: AppointmentStatus;
  notes?: string | null;
}

export interface AppointmentQuery extends PageQuery {
  doctorId?: number;
  patientId?: number;
  status?: AppointmentStatus;
  /**
   * Inclusive lower / exclusive upper bound on the appointment start, as a local
   * ISO string. The calendar asks for exactly the day or week on screen instead
   * of pulling a wide slice and filtering in the browser.
   */
  from?: string;
  to?: string;
}

export const APPOINTMENT_STATUSES: AppointmentStatus[] = [
  'Pending',
  'Confirmed',
  'Completed',
  'Cancelled'
];
