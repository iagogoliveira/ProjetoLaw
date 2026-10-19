import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  CaseClaimsResponse,
  ClaimInput,
  ClaimValueChangeResponse,
} from '../../../api/types.gen';
import type { ApiResult, FieldErrors } from '../../../core/data/api-result';
import { CasesApi } from './cases-api';

export const ClaimsStore = signalStore(
  withState({
    data: null as CaseClaimsResponse | null,
    history: [] as ClaimValueChangeResponse[],
    historyClaimId: null as string | null,
    busy: false,
    loading: false,
    errorCode: null as string | null,
    fieldErrors: undefined as FieldErrors | undefined,
  }),
  withMethods((store, api = inject(CasesApi)) => {
    let request = 0;
    let historyRequest = 0;
    let caseId = '';
    let date: string | null = null;

    async function load(id: string, updateTo: string | null): Promise<void> {
      caseId = id;
      date = updateTo;
      const current = ++request;
      patchState(store, { loading: true, errorCode: null });
      const result = await api.claims(id, updateTo);
      if (current === request) {
        patchState(
          store,
          result.ok
            ? { data: result.value, loading: false }
            : { errorCode: result.code, loading: false },
        );
      }
    }

    async function change(call: () => Promise<ApiResult<unknown>>): Promise<boolean> {
      if (store.busy()) return false;
      historyRequest++;
      patchState(store, { busy: true, errorCode: null, fieldErrors: undefined });
      const result = await call();
      if (result.ok) {
        patchState(store, { history: [], historyClaimId: null });
        await load(caseId, date);
      }
      patchState(store, {
        busy: false,
        ...(!result.ok ? { errorCode: result.code, fieldErrors: result.errors } : {}),
      });
      return result.ok;
    }

    async function showHistory(claimId: string): Promise<void> {
      if (store.busy()) return;
      const current = ++historyRequest;
      patchState(store, { history: [], historyClaimId: claimId, errorCode: null });
      const result = await api.claimHistory(caseId, claimId);
      if (current === historyRequest) {
        patchState(store, result.ok ? { history: result.value } : { errorCode: result.code });
      }
    }

    return {
      load,
      showHistory,
      save: (claimId: string | null, input: ClaimInput) =>
        change(() => api.saveClaim(caseId, claimId, input)),
      remove: (claimId: string) => change(() => api.deleteClaim(caseId, claimId)),
    };
  }),
);
