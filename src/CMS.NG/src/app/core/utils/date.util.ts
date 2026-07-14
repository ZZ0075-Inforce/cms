/**
 * Date helpers shared by every feature.
 *
 * Never use `d.toISOString().split('T')[0]` to serialise a date-only value: toISOString converts
 * to UTC first, so for a UTC+8 user a date picked as 2026-07-14 is sent as 2026-07-13.
 * Always build the string from local components.
 */
export function toIso(date: Date | null | undefined): string | null {
  if (!date) return null;
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Parses an ISO date string ("2026-07-14") into a local Date, for binding to p-datepicker. */
export function fromIso(iso: string | null | undefined): Date | null {
  if (!iso) return null;
  const [year, month, day] = iso.split('T')[0].split('-').map(Number);
  return new Date(year, month - 1, day);
}

/**
 * Dapper returns SQL `datetime` with Kind=Unspecified, so the JSON carries no timezone suffix
 * and the browser would read it as local time. Append 'Z' before parsing.
 */
export function fromUtcDateTime(value: string | null | undefined): Date | null {
  if (!value) return null;
  return new Date(value.endsWith('Z') ? value : `${value}Z`);
}

export function addYears(date: Date, years: number): Date {
  const result = new Date(date);
  result.setFullYear(result.getFullYear() + years);
  return result;
}
