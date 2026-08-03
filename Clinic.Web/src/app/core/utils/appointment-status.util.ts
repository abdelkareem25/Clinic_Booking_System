import { BadgeTone } from '../../shared/ui/data-table/data-table.model';
import { AppointmentTimeStatus } from '../models/appointment.model';

/**
 * Derives a time-based status (Upcoming / Today / Past) from an appointment's
 * ISO date string.
 *
 * The API has no status column, so status is a function of the clock rather
 * than stored state. That is honest — it can never disagree with the date —
 * but it also means "cancelled" cannot be represented, which is why cancelling
 * deletes the appointment.
 */
export function deriveAppointmentStatus(
  appointmentDate: string | null | undefined,
  now: Date = new Date()
): AppointmentTimeStatus {
  const timestamp = appointmentDate ? Date.parse(appointmentDate) : NaN;
  if (Number.isNaN(timestamp)) {
    return 'Upcoming';
  }

  const date = new Date(timestamp);
  if (isSameDay(date, now)) {
    return 'Today';
  }

  return timestamp >= now.getTime() ? 'Upcoming' : 'Past';
}

export function appointmentStatusTone(status: AppointmentTimeStatus): BadgeTone {
  switch (status) {
    case 'Today':
      return 'warning';
    case 'Upcoming':
      return 'success';
    case 'Past':
    default:
      return 'neutral';
  }
}

export function appointmentStatusLabel(status: AppointmentTimeStatus): string {
  switch (status) {
    case 'Today':
      return 'appointments.statusToday';
    case 'Upcoming':
      return 'appointments.statusUpcoming';
    case 'Past':
    default:
      return 'appointments.statusPast';
  }
}

function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}
