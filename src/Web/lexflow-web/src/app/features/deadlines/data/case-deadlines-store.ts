import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { DeadlineResponse } from '../../../api/types.gen';
import { DeadlinesApi } from './deadlines-api';

export const CaseDeadlinesStore = signalStore(
  withState({ items: [] as DeadlineResponse[], loading: false, errorCode: null as string | null }),
  withMethods((store, api = inject(DeadlinesApi)) => {
    let version = 0;
    return {
      async load(caseId: string): Promise<void> {
        const request = ++version;
        patchState(store, { items: [], loading: true, errorCode: null });
        const result = await api.caseDeadlines(caseId);
        if (request !== version) return;
        patchState(
          store,
          result.ok
            ? { loading: false, items: result.value }
            : { loading: false, errorCode: result.code },
        );
      },
    };
  }),
);
