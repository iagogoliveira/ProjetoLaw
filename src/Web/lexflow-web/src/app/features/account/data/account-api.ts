import { Injectable } from '@angular/core';
import {
  confirmTotp,
  enableEmailMfa,
  endMySession,
  getMe,
  listMySessions,
  startTotpSetup,
} from '../../../api/sdk.gen';
import type {
  MeResponse,
  RecoveryCodesResponse,
  SessionResponse,
  TotpSetupResponse,
} from '../../../api/types.gen';

/** Resultado de uma chamada: o valor, ou o `code` estável do ProblemDetails (traduzido em `errors.*`). */
export type ApiResult<T> = { ok: true; value: T } | { ok: false; code: string };

export const UNEXPECTED_ERROR = 'http.error';

/** Único ponto do feature que fala HTTP (seção 5.2), sobre o cliente gerado do OpenAPI. */
@Injectable({ providedIn: 'root' })
export class AccountApi {
  me(): Promise<ApiResult<MeResponse>> {
    return toResult(getMe());
  }

  sessions(): Promise<ApiResult<SessionResponse[]>> {
    return toResult(listMySessions());
  }

  endSession(id: string): Promise<ApiResult<unknown>> {
    return toResult(endMySession({ path: { id } }));
  }

  startTotp(): Promise<ApiResult<TotpSetupResponse>> {
    return toResult(startTotpSetup());
  }

  confirmTotp(code: string): Promise<ApiResult<RecoveryCodesResponse>> {
    return toResult(confirmTotp({ body: { code } }));
  }

  enableEmailMfa(): Promise<ApiResult<unknown>> {
    return toResult(enableEmailMfa());
  }
}

async function toResult<T>(call: Promise<{ data?: T; error?: unknown }>): Promise<ApiResult<T>> {
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
