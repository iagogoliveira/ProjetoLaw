import { describe, expect, it } from 'vitest';
import { localDateTime, utcDateTime } from '../data/schedule-format';
import { isEventRangeValid } from './event-validation';

describe('event date handling', () => {
  it('converts between office civil time and UTC independently of the browser time zone', () => {
    const local = '2026-10-22T10:15';
    const instant = utcDateTime(local);
    expect(instant).toBe('2026-10-22T13:15:00Z');
    expect(localDateTime(instant)).toBe(local);
  });

  it('requires the event end to be later than its start', () => {
    expect(isEventRangeValid('2026-10-22T10:00', '2026-10-22T10:01')).toBe(true);
    expect(isEventRangeValid('2026-10-22T10:00', '2026-10-22T10:00')).toBe(false);
    expect(isEventRangeValid('2026-10-22T10:00', '2026-10-22T09:59')).toBe(false);
    expect(isEventRangeValid('invalid', '2026-10-22T11:00')).toBe(false);
  });
});
