import type { PersonRoleKind } from '../../../api/types.gen';

/** Papéis na ordem em que a tela os oferece. */
export const PERSON_ROLES: readonly PersonRoleKind[] = [
  'client',
  'opponent',
  'witness',
  'expert',
  'correspondent',
  'supplier',
];

/** UFs aceitas nos endereços. */
export const BRAZILIAN_STATES: readonly string[] = [
  'AC',
  'AL',
  'AP',
  'AM',
  'BA',
  'CE',
  'DF',
  'ES',
  'GO',
  'MA',
  'MT',
  'MS',
  'MG',
  'PA',
  'PB',
  'PR',
  'PE',
  'PI',
  'RJ',
  'RN',
  'RS',
  'RO',
  'RR',
  'SC',
  'SP',
  'SE',
  'TO',
];

/** Só os dígitos do texto (CPF, CNPJ, CEP digitados com ou sem pontuação). */
export const digitsOf = (value: string | null | undefined): string =>
  (value ?? '').replace(/\D/g, '');

/** CPF (11 dígitos) ou CNPJ (14) com a pontuação de costume; outro tamanho volta como veio. */
export function formatDocument(document: string | null | undefined): string {
  const digits = digitsOf(document);
  if (digits.length === 11) {
    return digits.replace(/^(\d{3})(\d{3})(\d{3})(\d{2})$/, '$1.$2.$3-$4');
  }
  if (digits.length === 14) {
    return digits.replace(/^(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})$/, '$1.$2.$3/$4-$5');
  }
  return document ?? '';
}

/** CEP com hífen. */
export function formatPostalCode(postalCode: string): string {
  const digits = digitsOf(postalCode);
  return digits.length === 8 ? `${digits.slice(0, 5)}-${digits.slice(5)}` : postalCode;
}
