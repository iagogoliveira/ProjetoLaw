/** Erros por campo do ProblemDetails de validação (`errors`). */
export type FieldErrors = Record<string, readonly { code: string }[]>;

/**
 * Resultado de uma chamada: o valor, ou o `code` estável do ProblemDetails (traduzido em `errors.*`) e, em
 * validação, os erros por campo.
 */
export type ApiResult<T> =
  { ok: true; value: T } | { ok: false; code: string; errors?: FieldErrors };

export const UNEXPECTED_ERROR = 'http.error';

/** Converte a resposta do cliente gerado (`{ data, error }`) em {@link ApiResult}; usado só pelos serviços de `data/`. */
export async function toResult<T>(
  call: Promise<{ data?: T; error?: unknown }>,
): Promise<ApiResult<T>> {
  try {
    const { data, error } = await call;
    return error === undefined
      ? { ok: true, value: data as T }
      : { ok: false, code: codeOf(error), errors: fieldErrorsOf(error) };
  } catch (error) {
    return { ok: false, code: codeOf(error) };
  }
}

function fieldErrorsOf(error: unknown): FieldErrors | undefined {
  const body = (error as { error?: unknown } | null)?.error ?? error;
  const errors = (body as { errors?: unknown } | null)?.errors;
  return errors && typeof errors === 'object' ? (errors as FieldErrors) : undefined;
}

function codeOf(error: unknown): string {
  const body = (error as { error?: unknown } | null)?.error ?? error;
  const code = (body as { code?: unknown } | null)?.code;
  return typeof code === 'string' ? code : UNEXPECTED_ERROR;
}
