/** Resultado de uma chamada: o valor, ou o `code` estável do ProblemDetails (traduzido em `errors.*`). */
export type ApiResult<T> = { ok: true; value: T } | { ok: false; code: string };

export const UNEXPECTED_ERROR = 'http.error';

/** Converte a resposta do cliente gerado (`{ data, error }`) em {@link ApiResult}; usado só pelos serviços de `data/`. */
export async function toResult<T>(
  call: Promise<{ data?: T; error?: unknown }>,
): Promise<ApiResult<T>> {
  try {
    const { data, error } = await call;
    return error === undefined
      ? { ok: true, value: data as T }
      : { ok: false, code: codeOf(error) };
  } catch (error) {
    return { ok: false, code: codeOf(error) };
  }
}

function codeOf(error: unknown): string {
  const body = (error as { error?: unknown } | null)?.error ?? error;
  const code = (body as { code?: unknown } | null)?.code;
  return typeof code === 'string' ? code : UNEXPECTED_ERROR;
}
