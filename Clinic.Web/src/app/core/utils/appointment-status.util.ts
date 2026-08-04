import { BadgeTone } from '../../shared/ui/data-table/data-table.model';
import { AppointmentStatus, AppointmentTimeStatus } from '../models/appointment.model';

/**
 * Derives a time-based status (Upcoming / Today / Past) from an appointment's
 * ISO date string.
 *
 * This is the *time* dimension only. The API now also stores a lifecycle status
 * (Pending / Confirmed / Completed / Cancelled) — see `lifecycleStatusLabel`
 * below — and the two are deliberately separate: a cancelled appointment can
 * still be today, and no amount of clock-watching can tell you it was cancelled.
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

// -----------------------------------------------------------------------------
// Lifecycle status — stored, not derived
// -----------------------------------------------------------------------------

/**
 * Tone for the stored lifecycle status.
 *
 * Cancelled is `danger` rather than `neutral` on purpose: it is the one state
 * that means "do not expect this patient", and it has to be readable at a
 * glance from across the front desk.
 */
export function lifecycleStatusTone(status: AppointmentStatus): BadgeTone {
  switch (status) {
    case 'Confirmed':
      return 'success';
    case 'Completed':
      return 'info';
    case 'Cancelled':
      return 'danger';
    case 'Pending':
    default:
      return 'warning';
  }
}

export function lifecycleStatusLabel(status: AppointmentStatus): string {
  return `appointments.status${status}`;
}

function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}
