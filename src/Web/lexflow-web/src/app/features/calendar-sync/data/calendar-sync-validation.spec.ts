import { describe, expect, it } from 'vitest';
import { isCalendarSyncWindowValid } from './calendar-sync-validation';

describe('isCalendarSyncWindowValid', () => {
  it('accepts inclusive start and exclusive end windows of one through 366 calendar days', () => {
    expect(isCalendarSyncWindowValid('2026-01-01', '2026-01-02')).toBe(true);
    expect(isCalendarSyncWindowValid('2026-01-01', '2027-01-02')).toBe(true);
    expect(isCalendarSyncWindowValid('2026-01-01', '2027-01-03')).toBe(false);
  });

  it('rejects empty, reversed, and impossible civil dates', () => {
    expect(isCalendarSyncWindowValid('2026-01-01', '2026-01-01')).toBe(false);
    expect(isCalendarSyncWindowValid('2026-02-01', '2026-01-31')).toBe(false);
    expect(isCalendarSyncWindowValid('2026-02-30', '2026-03-02')).toBe(false);
  });
});
