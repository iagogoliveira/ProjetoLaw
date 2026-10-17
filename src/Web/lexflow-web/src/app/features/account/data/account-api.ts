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
import { ApiResult, toResult } from '../../../core/data/api-result';

export type { ApiResult } from '../../../core/data/api-result';

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
