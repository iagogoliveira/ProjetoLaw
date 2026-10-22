import { Temporal } from 'temporal-polyfill';
import type { AgendaItem } from '../../../api/types.gen';

export const OFFICE_ZONE = 'America/Sao_Paulo';
export function agendaWindow(day: string, view: string): { from: string; until: string } {
  let date = Temporal.PlainDate.from(day);
  if (view === 'dayGridMonth') date = date.with({ day: 1 });
  else if (view === 'timeGridWeek') date = date.subtract({ days: date.dayOfWeek - 1 });
  const end =
    view === 'dayGridMonth'
      ? date.add({ months: 1 })
      : date.add({ days: view === 'timeGridWeek' ? 7 : 1 });
  return { from: date.toString(), until: end.toString() };
}
export function civilToday(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: OFFICE_ZONE }).format(new Date());
}
export function formatInstant(instant: string): string {
  return new Intl.DateTimeFormat('pt-BR', {
    timeZone: OFFICE_ZONE,
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(instant));
}
export function agendaLink(item: AgendaItem): string | null {
  switch (item.kind) {
    case 'deadline':
      return `/prazos/${item.id}`;
    case 'task':
      return `/tarefas/${item.id}`;
    case 'event':
      return `/agenda/compromissos/${item.id}`;
    case 'hearing':
      return item.caseId ? `/processos/${item.caseId}` : null;
  }
}
export function localDateTime(instant: string): string {
  return Temporal.Instant.from(instant)
    .toZonedDateTimeISO(OFFICE_ZONE)
    .toPlainDateTime()
    .toString()
    .slice(0, 16);
}
export function utcDateTime(local: string): string {
  return Temporal.PlainDateTime.from(local)
    .toZonedDateTime(OFFICE_ZONE, { disambiguation: 'reject' })
    .toInstant()
    .toString();
}
export function validWindow(from: string, until: string): boolean {
  try {
    const start = Temporal.PlainDate.from(from);
    const end = Temporal.PlainDate.from(until);
    const days = start.until(end).days;
    return start.toString() === from && end.toString() === until && days > 0 && days <= 366;
  } catch {
    return false;
  }
}
