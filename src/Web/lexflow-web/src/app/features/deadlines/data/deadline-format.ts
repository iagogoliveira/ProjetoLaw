import type { WorkItemStatus } from '../../../api/types.gen';

export type DeadlineUrgency = 'red' | 'yellow' | 'green' | 'completed' | 'missed';

/** Civil dates are compared as ISO dates, independent of the browser's timezone. */
export function deadlineUrgency(
  item: { status: WorkItemStatus; fatalDueOn: string; internalDueOn: string },
  today: string,
): DeadlineUrgency {
  if (item.status === 'completed' || item.status === 'missed') return item.status;
  if (item.fatalDueOn <= today) return 'red';
  if (item.internalDueOn <= today || item.fatalDueOn <= addCivilDays(today, 2)) return 'yellow';
  return 'green';
}

export function addCivilDays(day: string, count: number): string {
  const date = new Date(`${day}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + count);
  return date.toISOString().slice(0, 10);
}

export function formatDate(day: string | null | undefined): string {
  return day ? day.slice(0, 10).split('-').reverse().join('/') : '—';
}

export function errorKey(code: string): string {
  const known = new Set([
    'scheduling.deadline_list_invalid',
    'scheduling.deadline_not_found',
    'scheduling.calculation_changed',
    'scheduling.deadline_review_required',
    'scheduling.deadline_invalid',
    'scheduling.separate_reviewer_required',
    'scheduling.evidence_required',
    'scheduling.missed_reason_required',
    'scheduling.assignee_unavailable',
    'scheduling.deadline_transition_invalid',
    'scheduling.calculation_invalid',
    'scheduling.calendar_out_of_range',
    'scheduling.court_not_found',
    'scheduling.case_unavailable',
    'authorization.forbidden',
    'auth.forbidden',
    'organization.not_a_member',
    'organization.mfa_required',
    'validation.failed',
  ]);
  return known.has(code)
    ? `deadlines.errors.${code.replaceAll('.', '_')}`
    : 'deadlines.errors.generic';
}
