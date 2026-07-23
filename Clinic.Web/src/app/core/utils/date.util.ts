/** Formats a Date as a timezone-safe `YYYY-MM-DD` string (date only). */
export function toDateOnly(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Combines a Date and an `HH:mm` time string into a local ISO date-time string. */
export function combineDateAndTime(date: Date, time: string): string {
  const [hours = '0', minutes = '0'] = time.split(':');
  const result = new Date(date);
  result.setHours(Number(hours), Number(minutes), 0, 0);
  return toLocalIso(result);
}

/** ISO 8601 without a trailing `Z`, preserving the wall-clock time the user picked. */
export function toLocalIso(date: Date): string {
  const pad = (value: number): string => `${value}`.padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}:00`
  );
}

/** Safely parses an ISO string into a Date, or null when invalid. */
export function parseDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp) ? null : new Date(timestamp);
}
