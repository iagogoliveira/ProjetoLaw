import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { CaptureAlertResponse } from '../../../api/types.gen';
import { PublicationsApi } from './publications-api';

export const CaptureAlertsStore = signalStore(
  withState({
    items: [] as CaptureAlertResponse[],
    loading: false,
    errorCode: null as string | null,
  }),
  withMethods((store, api = inject(PublicationsApi)) => {
    let version = 0;
    return {
      async load(day: string): Promise<void> {
        const request = ++version;
        patchState(store, { items: [], loading: true, errorCode: null });
        const result = await api.alerts(day);
        if (request !== version) return;
        patchState(
          store,
          result.ok
            ? { items: result.value, loading: false }
            : { errorCode: result.code, loading: false },
        );
      },
    };
  }),
);
