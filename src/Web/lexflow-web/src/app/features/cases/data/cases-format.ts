import { digitsOf } from '../../../shared/br-format';

/** Número CNJ com a pontuação (`NNNNNNN-DD.AAAA.J.TR.OOOO`); outros valores ficam como vieram. */
export function formatCnj(value: string | null | undefined): string {
  const digits = digitsOf(value);
  if (digits.length !== 20) {
    return value ?? '';
  }

  return `${digits.slice(0, 7)}-${digits.slice(7, 9)}.${digits.slice(9, 13)}.${digits[13]}.${digits.slice(14, 16)}.${digits.slice(16)}`;
}

/** Os tipos de acontecimento da linha do tempo, para o filtro da aba (prefixos dos códigos). */
export const TIMELINE_GROUPS = [
  'litigation.case',
  'litigation.status',
  'litigation.claim',
  'litigation.risk',
  'litigation.settlement',
  'litigation.hearing',
] as const;
