import { describe, expect, it } from 'vitest';
import {
  agendaWindow,
  formatInstant,
  localDateTime,
  utcDateTime,
  validWindow,
} from './schedule-format';

describe('Scheduling civil dates', () => {
  it('converts office time independently of the browser timezone and preserves instants', () => {
    expect(utcDateTime('2026-10-22T09:30')).toBe('2026-10-22T12:30:00Z');
    expect(localDateTime('2026-10-22T12:30:00Z')).toBe('2026-10-22T09:30');
    expect(formatInstant('2026-10-22T12:30:00Z')).toContain('09:30');
  });
  it('uses civil Monday weeks, real months and exclusive ends across year and leap boundaries', () => {
    expect(agendaWindow('2027-01-01', 'timeGridWeek')).toEqual({
      from: '2026-12-28',
      until: '2027-01-04',
    });
    expect(agendaWindow('2028-02-29', 'dayGridMonth')).toEqual({
      from: '2028-02-01',
      until: '2028-03-01',
    });
    expect(agendaWindow('2028-02-29', 'timeGridDay')).toEqual({
      from: '2028-02-29',
      until: '2028-03-01',
    });
    expect(validWindow('2026-02-30', '2026-03-10')).toBe(false);
    expect(validWindow('2026-01-01', '2028-01-01')).toBe(false);
  });
});
