import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { ConflictCheckResponse, ConflictRisk } from '../../../api/types.gen';
import { ConflictQuery, CrmApi } from './crm-api';

interface ConflictState {
  /** A verificação feita nesta tela (o resultado e, depois, a decisão de prosseguir). */
  result: ConflictCheckResponse | null;
  report: ConflictCheckResponse[];
  nextCursor: string | null;
  risk: ConflictRisk | null;
  busy: boolean;
  errorCode: string | null;
}

/**
 * Conflito de interesses (RF-07): verificar nome, documento e e-mails do lado escolhido, prosseguir (com
 * justificativa quando há alerta) e o relatório das verificações para auditoria e compliance da OAB.
 */
export const ConflictStore = signalStore(
  withState<ConflictState>({
    result: null,
    report: [],
    nextCursor: null,
    risk: null,
    busy: false,
    errorCode: null,
  }),
  withMethods((store, api = inject(CrmApi)) => {
    async function check(query: ConflictQuery): Promise<void> {
      patchState(store, { busy: true, errorCode: null, result: null });
      const result = await api.checkConflict(query);
      patchState(
        store,
        result.ok ? { busy: false, result: result.value } : { busy: false, errorCode: result.code },
      );
    }

    async function proceed(justification: string | null): Promise<boolean> {
      const current = store.result();
      if (!current) {
        return false;
      }

      patchState(store, { busy: true, errorCode: null });
      const result = await api.proceed(current.id, justification?.trim() || null);
      patchState(
        store,
        result.ok ? { busy: false, result: result.value } : { busy: false, errorCode: result.code },
      );
      return result.ok;
    }

    function reset(): void {
      patchState(store, { result: null, errorCode: null });
    }

    async function loadReport(risk: ConflictRisk | null): Promise<void> {
      patchState(store, { risk, busy: true, errorCode: null });
      const result = await api.conflictChecks(risk, null);
      patchState(
        store,
        result.ok
          ? { busy: false, report: result.value.items, nextCursor: result.value.nextCursor }
          : { busy: false, report: [], nextCursor: null, errorCode: result.code },
      );
    }

    async function moreReport(): Promise<void> {
      const cursor = store.nextCursor();
      if (!cursor || store.busy()) {
        return;
      }

      patchState(store, { busy: true });
      const result = await api.conflictChecks(store.risk(), cursor);
      patchState(
        store,
        result.ok
          ? {
              busy: false,
              report: [...store.report(), ...result.value.items],
              nextCursor: result.value.nextCursor,
            }
          : { busy: false, errorCode: result.code },
      );
    }

    return { check, proceed, reset, loadReport, moreReport };
  }),
);
