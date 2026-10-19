import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  OfferInput,
  PayInstallmentRequest,
  SettlementResponse,
  StartSettlementRequest,
  SubmitSettlementRequest,
} from '../../../api/types.gen';
import type { FieldErrors } from '../../../core/data/api-result';
import { CasesApi } from './cases-api';

export const SettlementsStore = signalStore(
  withState({
    items: [] as SettlementResponse[],
    busy: false,
    errorCode: null as string | null,
    fieldErrors: undefined as FieldErrors | undefined,
  }),
  withMethods((store, api = inject(CasesApi)) => {
    let caseId = '';
    async function load(id: string): Promise<void> {
      caseId = id;
      const result = await api.settlements(id);
      patchState(
        store,
        result.ok ? { items: result.value, errorCode: null } : { errorCode: result.code },
      );
    }
    async function change(call: () => ReturnType<CasesApi['startSettlement']>): Promise<boolean> {
      if (store.busy()) return false;
      patchState(store, { busy: true, errorCode: null, fieldErrors: undefined });
      const result = await call();
      patchState(
        store,
        result.ok
          ? {
              busy: false,
              items: [result.value, ...store.items().filter((s) => s.id !== result.value.id)],
            }
          : { busy: false, errorCode: result.code, fieldErrors: result.errors },
      );
      return result.ok;
    }
    return {
      load,
      start: (input: StartSettlementRequest) => change(() => api.startSettlement(caseId, input)),
      offer: (id: string, input: OfferInput) => change(() => api.offer(caseId, id, input)),
      submit: (id: string, input: SubmitSettlementRequest) =>
        change(() => api.submitSettlement(caseId, id, input)),
      step: (
        id: string,
        step: 'approve' | 'return' | 'reject' | 'cancel' | 'homologate',
        value = '',
      ) => change(() => api.settlementStep(caseId, id, step, value)),
      pay: (id: string, installmentId: string, input: PayInstallmentRequest) =>
        change(() => api.payInstallment(caseId, id, installmentId, input)),
    };
  }),
);
