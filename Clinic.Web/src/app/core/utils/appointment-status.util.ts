import { AppointmentTimeStatus } from '../models/appointment.model';
import { ChipTone } from '../../shared/components/data-table/data-table.model';

/**
 * Derives a time-based status (Upcoming / Today / Past) from an appointment's
 * ISO date string. The backend does not expose a status column.
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

export function appointmentStatusTone(status: AppointmentTimeStatus): ChipTone {
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

function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}
