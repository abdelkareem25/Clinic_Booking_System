import { CalendarItem, PositionedItem, TimeInterval } from './calendar.model';

/**
 * Grid geometry.
 *
 * The rows are 30 minutes because that is the granularity a front desk reads a
 * day at. It is deliberately *not* `ClinicSettings.slotMinutes`: that setting
 * governs what may be booked, and a clinic booking in 15-minute units still
 * wants half-hour gridlines rather than 48 unlabelled rows. Block heights come
 * from each appointment's own duration, so a 15- or 45-minute appointment still
 * draws at its true length against a 30-minute grid.
 */
export const GRID_SLOT_MINUTES = 30;

/** Height of one 30-minute row. Mirrored by `--slot-h` in the stylesheet. */
export const SLOT_HEIGHT_PX = 56;

/** Minutes -> pixels, the one conversion the whole grid is built on. */
export function minutesToPx(minutes: number): number {
  return (minutes / GRID_SLOT_MINUTES) * SLOT_HEIGHT_PX;
}

/** Pixels -> minutes, snapped to the nearest gridline. Used when a drag lands. */
export function pxToSnappedMinutes(px: number): number {
  return Math.round(px / SLOT_HEIGHT_PX) * GRID_SLOT_MINUTES;
}

export function floorToSlot(minutes: number): number {
  return Math.floor(minutes / GRID_SLOT_MINUTES) * GRID_SLOT_MINUTES;
}

export function ceilToSlot(minutes: number): number {
  return Math.ceil(minutes / GRID_SLOT_MINUTES) * GRID_SLOT_MINUTES;
}

/** The rows to draw, as minutes since midnight. */
export function buildSlots(startMinutes: number, endMinutes: number): number[] {
  const slots: number[] = [];
  for (let minute = startMinutes; minute < endMinutes; minute += GRID_SLOT_MINUTES) {
    slots.push(minute);
  }
  return slots;
}

/**
 * The window of the day the grid covers.
 *
 * Wide enough to show every working hour and every appointment, and no wider —
 * rendering midnight to midnight would put the working day in the middle of two
 * screens of empty rows. Appointments are included even when they fall outside
 * published hours, because rows predating the working-hours rule exist and an
 * appointment the calendar cannot draw is worse than an early gridline.
 */
export function visibleRange(
  intervals: readonly TimeInterval[],
  items: readonly CalendarItem[],
  fallback: TimeInterval
): TimeInterval {
  let start = Number.POSITIVE_INFINITY;
  let end = Number.NEGATIVE_INFINITY;

  for (const interval of intervals) {
    start = Math.min(start, interval.start);
    end = Math.max(end, interval.end);
  }

  for (const item of items) {
    start = Math.min(start, item.startMinutes);
    end = Math.max(end, item.endMinutes);
  }

  if (!Number.isFinite(start) || !Number.isFinite(end) || start >= end) {
    start = fallback.start;
    end = fallback.end;
  }

  return {
    // Floored to the hour, not to the slot, so the first gridline is a labelled
    // hour and the hour rules stay on the hour. A grid starting at 09:30 would
    // put every "stronger" hour line half a row out.
    start: Math.max(0, Math.floor(start / 60) * 60),
    // One row of headroom so the last appointment is not flush against the edge.
    end: Math.min(24 * 60, ceilToSlot(end) + GRID_SLOT_MINUTES),
  };
}

/**
 * The gaps between a lane's working intervals — lunch, a split shift.
 *
 * Shaded differently from "closed": a break is time the doctor is on site but
 * not seeing patients, and staff read the two differently when they are looking
 * for somewhere to squeeze an urgent case.
 */
export function breakIntervals(intervals: readonly TimeInterval[]): TimeInterval[] {
  const sorted = [...intervals].sort((a, b) => a.start - b.start);
  const breaks: TimeInterval[] = [];

  for (let index = 1; index < sorted.length; index += 1) {
    const previousEnd = sorted[index - 1].end;
    const nextStart = sorted[index].start;
    if (nextStart > previousEnd) {
      breaks.push({ start: previousEnd, end: nextStart });
    }
  }

  return breaks;
}

/** Merges overlapping/touching intervals so shading never double-paints. */
export function mergeIntervals(intervals: readonly TimeInterval[]): TimeInterval[] {
  const sorted = [...intervals].sort((a, b) => a.start - b.start);
  const merged: TimeInterval[] = [];

  for (const interval of sorted) {
    const last = merged[merged.length - 1];
    if (last && interval.start <= last.end) {
      last.end = Math.max(last.end, interval.end);
    } else {
      merged.push({ ...interval });
    }
  }

  return merged;
}

/** True when `minutes` falls inside any of the intervals. */
export function withinIntervals(
  intervals: readonly TimeInterval[],
  start: number,
  end: number
): boolean {
  return intervals.some((interval) => start >= interval.start && end <= interval.end);
}

/**
 * Resolves a lane's appointments into boxes, side by side where they overlap.
 *
 * Two appointments on one resource is not supposed to happen — the API rejects
 * an overlap — but it does happen: rows created before that rule existed, and
 * rows whose `EndTime` was never populated. Stacking them on top of each other
 * would hide one of them entirely, which is the one outcome a scheduling screen
 * must never produce. So overlapping appointments share the lane's width.
 *
 * The algorithm is the standard calendar sweep: collect a *cluster* of
 * appointments that transitively overlap, give each the first column free at its
 * start time, then size every box in the cluster to `1 / columns` of the lane.
 * Clustering rather than pairwise comparison is what keeps three-deep overlaps
 * aligned instead of drifting.
 */
export function positionLane(
  items: readonly CalendarItem[],
  rangeStartMinutes: number
): PositionedItem[] {
  const sorted = [...items].sort(
    (a, b) => a.startMinutes - b.startMinutes || b.endMinutes - a.endMinutes
  );

  const positioned: PositionedItem[] = [];
  let cluster: CalendarItem[] = [];
  /** End time of the appointment currently occupying each column. */
  let columnEnds: number[] = [];
  const columnOf = new Map<CalendarItem, number>();

  const flush = (): void => {
    if (!cluster.length) {
      return;
    }

    const columns = columnEnds.length;
    const widthPct = 100 / columns;

    for (const item of cluster) {
      const column = columnOf.get(item) ?? 0;
      positioned.push({
        ...item,
        topPx: minutesToPx(item.startMinutes - rangeStartMinutes),
        // A hairline floor so a 5-minute appointment stays clickable and its
        // text stays legible; the true duration still drives everything longer.
        heightPx: Math.max(minutesToPx(item.durationMinutes), 26),
        widthPct,
        offsetPct: column * widthPct,
        // Later columns sit above earlier ones, so a narrow box's border reads
        // against its neighbour instead of being clipped by it.
        zIndex: column + 1,
        clusterSize: columns,
      });
    }

    cluster = [];
    columnEnds = [];
    columnOf.clear();
  };

  for (const item of sorted) {
    const clusterEnd = columnEnds.length ? Math.max(...columnEnds) : -Infinity;

    // Nothing in the current cluster is still running: start a fresh one, so an
    // unrelated later appointment is not narrowed by an earlier collision.
    if (item.startMinutes >= clusterEnd) {
      flush();
    }

    let column = columnEnds.findIndex((end) => end <= item.startMinutes);
    if (column === -1) {
      column = columnEnds.length;
    }

    columnEnds[column] = item.endMinutes;
    columnOf.set(item, column);
    cluster.push(item);
  }

  flush();

  return positioned;
}
