export { digitsOf, formatDocument, formatPostalCode } from '../../../shared/br-format';
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
