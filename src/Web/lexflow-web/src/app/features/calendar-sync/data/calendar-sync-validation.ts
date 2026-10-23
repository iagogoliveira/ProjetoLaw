import { Temporal } from 'temporal-polyfill';

export function isCalendarSyncWindowValid(from: string, until: string): boolean {
  try {
    const start = Temporal.PlainDate.from(from);
    const end = Temporal.PlainDate.from(until);
    const days = start.until(end, { largestUnit: 'day' }).days;
    return start.toString() === from && end.toString() === until && days >= 1 && days <= 366;
  } catch {
    return false;
  }
}
