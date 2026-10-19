import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  CaseRiskResponse,
  ClaimResponse,
  RiskAssessmentResponse,
  RiskInput,
} from '../../../api/types.gen';
import type { FieldErrors } from '../../../core/data/api-result';
import { CasesApi } from './cases-api';

export const RiskStore = signalStore(
  withState({
    data: null as CaseRiskResponse | null,
    claims: [] as ClaimResponse[],
    history: [] as RiskAssessmentResponse[],
    busy: false,
    errorCode: null as string | null,
    fieldErrors: undefined as FieldErrors | undefined,
  }),
  withMethods((store, api = inject(CasesApi)) => {
    let caseId = '';
    async function load(id: string): Promise<void> {
      caseId = id;
      const [risk, history, claims] = await Promise.all([
        api.risk(id),
        api.riskHistory(id),
        api.claims(id, null),
      ]);
      patchState(store, {
        data: risk.ok ? risk.value : null,
        history: history.ok ? history.value : [],
        claims: claims.ok ? claims.value.items : [],
        errorCode: !risk.ok
          ? risk.code
          : !history.ok
            ? history.code
            : !claims.ok
              ? claims.code
              : null,
      });
    }
    async function assess(input: RiskInput): Promise<boolean> {
      if (store.busy()) return false;
      patchState(store, { busy: true, errorCode: null, fieldErrors: undefined });
      const result = await api.assessRisk(caseId, input);
      if (result.ok) await load(caseId);
      patchState(store, {
        busy: false,
        ...(!result.ok ? { errorCode: result.code, fieldErrors: result.errors } : {}),
      });
      return result.ok;
    }
    return { load, assess };
  }),
);
