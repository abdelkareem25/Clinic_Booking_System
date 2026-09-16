import { Appointment } from '../../../core/models/appointment.model';

/** Day is the working view; week is the planning view. */
export type CalendarView = 'day' | 'week';

/**
 * What a calendar column books against.
 *
 * The reference design columns are treatment chairs. This system has no Chair or
 * Room entity, and inventing one — a table, a migration, an admin screen — to
 * satisfy a layout would be the wrong order to build in. What it *does* have is
 * the thing a chair stands in for: a resource that can hold one appointment at a
 * time, which here is the doctor. The API enforces exactly that, rejecting an
 * overlap per doctor.
 *
 * So resources are an abstraction with one implementation today. When a Chair or
 * Room entity arrives, it becomes a second `CalendarResourceProvider`; the grid,
 * the layout engine and the drag handler do not change, because none of them
 * knows what a resource is beyond its id.
 */
export type CalendarResourceKind = 'doctor' | 'chair' | 'room';

export interface CalendarResource {
  /** Stable within a kind. Today `doctor-7`; later `chair-3`. */
  id: string;
  kind: CalendarResourceKind;
  /** Display name, already localised where the source is a code. */
  name: string;
  /** Second line under the column heading — the speciality, today. */
  sublabel: string | null;
  /**
   * The doctor this column books against.
   *
   * Non-null for every resource the current API can schedule. A future chair
   * column still needs it: the booking rules, the working hours and the overlap
   * check are all per doctor, so a chair that is not tied to one cannot be
   * validated and must not accept a drop.
   */
  doctorId: number | null;
}

/**
 * Supplies the columns and says which column an appointment belongs in.
 *
 * The one seam a Chair/Room entity has to be threaded through.
 */
export interface CalendarResourceProvider {
  readonly kind: CalendarResourceKind;
  /** The columns to draw, in display order. */
  resources(): CalendarResource[];
  /** The column an appointment sits in, or null when it belongs to none on screen. */
  resourceIdOf(appointment: Appointment): string | null;
}

/**
 * A column of the grid.
 *
 * Day view lanes are resources; week view lanes are days. Keeping both behind
 * one shape is what lets a single grid render both views — the difference
 * between them is which lanes are built, not how they are drawn.
 */
export interface CalendarLane {
  id: string;
  /** Localised heading. */
  label: string;
  sublabel: string | null;
  /** Set on a week-view lane. */
  date: Date | null;
  /** Set on a day-view lane. */
  resource: CalendarResource | null;
  /** True for the lane representing today, so the header can mark it. */
  isToday: boolean;
  /**
   * Minutes-since-midnight ranges the doctor(s) behind this lane actually work.
   * Everything outside is shaded as closed; a gap between two ranges is a break.
   */
  workingIntervals: readonly TimeInterval[];
}

export interface TimeInterval {
  start: number;
  end: number;
}

/** An appointment placed on the grid, before overlap resolution. */
export interface CalendarItem {
  appointment: Appointment;
  laneId: string;
  /** Minutes since midnight. */
  startMinutes: number;
  endMinutes: number;
  durationMinutes: number;
  /** The calendar day the appointment falls on. */
  date: Date;
}

/** An appointment with its box resolved, ready to render. */
export interface PositionedItem extends CalendarItem {
  topPx: number;
  heightPx: number;
  /** Percentage of the lane width, so the lane can be any size. */
  widthPct: number;
  offsetPct: number;
  zIndex: number;
  /** How many appointments share this item's time span, including itself. */
  clusterSize: number;
}

/** Where a drop landed. `laneId` identifies the target column. */
export interface CalendarDrop {
  appointment: Appointment;
  laneId: string;
  startMinutes: number;
  date: Date;
}
