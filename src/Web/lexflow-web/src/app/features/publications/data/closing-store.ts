import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { DailyCloseStatusResponse } from '../../../api/types.gen';
import { PublicationsApi } from './publications-api';

export const ClosingStore = signalStore(
  withState({
    day: '',
    state: null as DailyCloseStatusResponse | null,
    loading: false,
    busy: false,
    errorCode: null as string | null,
  }),
  withMethods((store, api = inject(PublicationsApi)) => {
    let version = 0;
    async function load(day: string): Promise<void> {
      const request = ++version;
      patchState(store, { day, state: null, loading: true, errorCode: null });
      const result = await api.closing(day);
      if (request !== version) return;
      patchState(
        store,
        result.ok
          ? { state: result.value, loading: false }
          : { errorCode: result.code, loading: false },
      );
    }
    return {
      load,
      async more(): Promise<void> {
        const revision = store.state()?.nextRevision;
        if (revision == null || store.loading()) return;
        const request = version;
        patchState(store, { loading: true, errorCode: null });
        const result = await api.closing(store.day(), Number(revision));
        if (request !== version) return;
        patchState(
          store,
          result.ok
            ? {
                state: {
                  ...result.value,
                  history: [...store.state()!.history, ...result.value.history],
                },
                loading: false,
              }
            : { loading: false, errorCode: result.code },
        );
      },
      async close(): Promise<boolean> {
        if (store.busy()) return false;
        const day = store.day();
        patchState(store, { busy: true, errorCode: null });
        const result = await api.close(day);
        patchState(store, { busy: false });
        if (!result.ok) {
          patchState(store, { errorCode: result.code });
          return false;
        }
        if (day === store.day()) await load(day);
        return true;
      },
    };
  }),
);
