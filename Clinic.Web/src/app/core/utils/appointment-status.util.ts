import { BadgeTone } from '../../shared/ui/data-table/data-table.model';
import { IconName } from '../../shared/ui/icon/icon.registry';
import {
  APPOINTMENT_STATUSES,
  AppointmentStatus,
  AppointmentTimeStatus,
} from '../models/appointment.model';

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

// -----------------------------------------------------------------------------
// Presentation
// -----------------------------------------------------------------------------

/**
 * Everything the UI needs to know about a status, in one place.
 *
 * The calendar draws a coloured block, the legend draws a swatch, the badge
 * draws a chip and the quick view draws an icon — four renderings of the same
 * fact. Defining them here means re-tinting "Cancelled" is one edit, and it is
 * impossible for the legend to disagree with the blocks it explains.
 *
 * `accent` and `surface` are design-token references, never raw colour: the
 * dark theme re-points the same names, so the calendar follows the theme without
 * a second palette.
 *
 * The statuses are exactly the API's `AppointmentStatus` enum. The reference
 * design shows richer states (arrived, in progress, no-show); inventing them
 * here would produce a legend describing something the backend cannot store.
 */
export interface AppointmentStatusConfig {
  status: AppointmentStatus;
  /** Translation key. */
  label: string;
  tone: BadgeTone;
  icon: IconName;
  /** The block's left/right edge and text accent. */
  accent: string;
  /** The block's fill. */
  surface: string;
}

export const APPOINTMENT_STATUS_CONFIG: Record<AppointmentStatus, AppointmentStatusConfig> = {
  Pending: {
    status: 'Pending',
    label: 'appointments.statusPending',
    tone: 'warning',
    icon: 'clock',
    accent: 'var(--c-warning)',
    surface: 'var(--c-warning-soft)',
  },
  Confirmed: {
    status: 'Confirmed',
    label: 'appointments.statusConfirmed',
    tone: 'success',
    icon: 'calendarCheck',
    accent: 'var(--c-success)',
    surface: 'var(--c-success-soft)',
  },
  Completed: {
    status: 'Completed',
    label: 'appointments.statusCompleted',
    tone: 'info',
    icon: 'check',
    accent: 'var(--c-accent)',
    surface: 'var(--c-accent-soft)',
  },
  Cancelled: {
    status: 'Cancelled',
    label: 'appointments.statusCancelled',
    tone: 'danger',
    icon: 'calendarX',
    accent: 'var(--c-danger)',
    surface: 'var(--c-danger-soft)',
  },
};

/** The legend, in lifecycle order. */
export const APPOINTMENT_STATUS_LEGEND: AppointmentStatusConfig[] = APPOINTMENT_STATUSES.map(
  (status) => APPOINTMENT_STATUS_CONFIG[status]
);

/** Tolerates an unknown value from an older or newer API rather than throwing. */
export function statusConfig(status: AppointmentStatus | null | undefined): AppointmentStatusConfig {
  return APPOINTMENT_STATUS_CONFIG[status as AppointmentStatus] ?? APPOINTMENT_STATUS_CONFIG.Pending;
}

function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}
