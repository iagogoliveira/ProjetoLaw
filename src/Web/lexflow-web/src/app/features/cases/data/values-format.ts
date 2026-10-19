import type { InstallmentInput } from '../../../api/types.gen';

export const CASE_INSTANCES = ['first', 'second', 'superior', 'supreme'] as const;
export const MONEY_PATTERN = /^\d+(?:\.\d{3})*(?:,\d{1,2})?$/;

export function parseAmount(value: string): number | null {
  return value.trim() && MONEY_PATTERN.test(value.trim())
    ? Number(value.trim().replace(/\./g, '').replace(',', '.'))
    : null;
}

export function editAmount(value: number | string | null): string {
  return value === null ? '' : Number(value).toFixed(2).replace('.', ',');
}

/** Datas civis e horários exibidos no fuso legal, inclusive para usuários fora de São Paulo. */
export function today(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date());
}

export function localDateTime(value: string): string {
  const parts = new Intl.DateTimeFormat('sv-SE', {
    timeZone: 'America/Sao_Paulo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).format(new Date(value));
  return parts.replace(' ', 'T');
}

export function hearingInstant(value: string): string {
  const civil = new Date(value + ':00Z').getTime();
  let instant = civil;
  // Usa as regras históricas do fuso, inclusive o horário de verão anterior a 2019.
  for (let attempt = 0; attempt < 2; attempt++) {
    const observed = new Date(localDateTime(new Date(instant).toISOString()) + ':00Z').getTime();
    instant += civil - observed;
  }
  return new Date(instant).toISOString();
}

/** Datas mensais ancoradas no primeiro vencimento; o fim do mês não desloca os meses seguintes. */
export function generateInstallments(
  amount: number,
  count: number,
  firstDue: string,
  interval: number,
): InstallmentInput[] {
  const cents = Math.round(amount * 100);
  if (
    !Number.isSafeInteger(cents) ||
    cents < count ||
    !Number.isInteger(count) ||
    count < 1 ||
    count > 360 ||
    !Number.isInteger(interval) ||
    interval < 1 ||
    interval > 120 ||
    !/^\d{4}-\d{2}-\d{2}$/.test(firstDue)
  )
    return [];
  const [year, month, day] = firstDue.split('-').map(Number);
  const base = Math.floor(cents / count);
  return Array.from({ length: count }, (_, i) => {
    const date = new Date(Date.UTC(year, month - 1 + i * interval, 1));
    const last = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate();
    date.setUTCDate(Math.min(day, last));
    return {
      dueOn: date.toISOString().slice(0, 10),
      amount: ((i === count - 1 ? cents - base * (count - 1) : base) / 100).toFixed(2),
    };
  });
}
