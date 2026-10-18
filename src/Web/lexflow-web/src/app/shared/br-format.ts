// Formatos brasileiros usados nas telas (documentos e CEP), sem dependência de feature.

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
