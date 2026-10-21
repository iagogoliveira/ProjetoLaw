import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { CalculateDeadlineQuery, DeadlineCalculationResponse } from '../../../api/types.gen';
import { DeadlinesApi } from './deadlines-api';

export interface DeadlineSuggestion {
  query: CalculateDeadlineQuery;
  result: DeadlineCalculationResponse;
}

export const DeadlineCalculationStore = signalStore(
  withState({
    suggestion: null as DeadlineSuggestion | null,
    busy: false,
    errorCode: null as string | null,
  }),
  withMethods((store, api = inject(DeadlinesApi)) => {
    let version = 0;
    function invalidate(): void {
      version++;
      patchState(store, { suggestion: null, busy: false, errorCode: null });
    }
    return {
      invalidate,
      async calculate(query: CalculateDeadlineQuery): Promise<void> {
        const request = ++version;
        patchState(store, { suggestion: null, busy: true, errorCode: null });
        const response = await api.calculate(query);
        if (request !== version) return;
        patchState(
          store,
          response.ok
            ? { busy: false, suggestion: { query, result: response.value } }
            : { busy: false, errorCode: response.code },
        );
      },
    };
  }),
);
