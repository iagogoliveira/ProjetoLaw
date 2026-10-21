import { describe, expect, it } from 'vitest';
import { addCivilDays, deadlineUrgency, formatDate } from './deadline-format';

describe('deadline urgency', () => {
  const today = '2026-10-21';
  const open = {
    status: 'pending' as const,
    fatalDueOn: '2026-10-30',
    internalDueOn: '2026-10-28',
  };
  it('distinguishes fatal due today, the two-day warning and the internal target', () => {
    expect(deadlineUrgency(open, today)).toBe('green');
    expect(deadlineUrgency({ ...open, fatalDueOn: today }, today)).toBe('red');
    expect(deadlineUrgency({ ...open, fatalDueOn: '2026-10-20' }, today)).toBe('red');
    expect(deadlineUrgency({ ...open, fatalDueOn: '2026-10-23' }, today)).toBe('yellow');
    expect(deadlineUrgency({ ...open, fatalDueOn: '2026-10-24' }, today)).toBe('green');
    expect(deadlineUrgency({ ...open, internalDueOn: today }, today)).toBe('yellow');
  });
  it('preserves terminal status regardless of past dates', () => {
    expect(deadlineUrgency({ ...open, status: 'completed', fatalDueOn: '2020-01-01' }, today)).toBe(
      'completed',
    );
    expect(deadlineUrgency({ ...open, status: 'missed' }, today)).toBe('missed');
  });
  it('handles month and year boundaries without local timezone conversion', () => {
    expect(addCivilDays('2026-12-31', 2)).toBe('2027-01-02');
    expect(addCivilDays('2028-02-28', 2)).toBe('2028-03-01');
    expect(formatDate('2026-10-21')).toBe('21/10/2026');
  });
});
