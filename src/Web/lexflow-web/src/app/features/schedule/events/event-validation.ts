import { Temporal } from 'temporal-polyfill';

export function isEventRangeValid(startsAt: string, endsAt: string): boolean {
  try {
    return (
      Temporal.PlainDateTime.compare(
        Temporal.PlainDateTime.from(endsAt),
        Temporal.PlainDateTime.from(startsAt),
      ) > 0
    );
  } catch {
    return false;
  }
}
