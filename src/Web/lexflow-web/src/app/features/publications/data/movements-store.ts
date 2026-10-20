import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { MovementInput, MovementResponse, MovementSyncResponse } from '../../../api/types.gen';
import { PublicationsApi } from './publications-api';

export const MovementsStore = signalStore(
  withState({
    caseId: '',
    items: [] as MovementResponse[],
    nextCursor: null as string | null,
    sync: null as MovementSyncResponse | null,
    loading: false,
    busy: false,
    errorCode: null as string | null,
  }),
  withMethods((store, api = inject(PublicationsApi)) => {
    let version = 0;
    async function load(id: string): Promise<void> {
      const request = ++version;
      patchState(store, {
        caseId: id,
        items: [],
        nextCursor: null,
        sync: null,
        loading: true,
        errorCode: null,
      });
      const [page, sync] = await Promise.all([api.movements(id, null), api.sync(id)]);
      if (request !== version) return;
      patchState(store, {
        loading: false,
        items: page.ok ? page.value.items : [],
        nextCursor: page.ok ? page.value.nextCursor : null,
        sync: sync.ok ? sync.value : null,
        errorCode: !page.ok ? page.code : !sync.ok ? sync.code : null,
      });
    }
    return {
      load,
      async more(): Promise<void> {
        const cursor = store.nextCursor();
        if (!cursor || store.loading()) return;
        const request = version;
        patchState(store, { loading: true });
        const page = await api.movements(store.caseId(), cursor);
        if (request !== version) return;
        patchState(
          store,
          page.ok
            ? {
                items: [...store.items(), ...page.value.items],
                nextCursor: page.value.nextCursor,
                loading: false,
              }
            : { loading: false, errorCode: page.code },
        );
      },
      async save(input: MovementInput): Promise<boolean> {
        if (store.busy()) return false;
        patchState(store, { busy: true, errorCode: null });
        const result = await api.createMovement(store.caseId(), input);
        patchState(store, { busy: false });
        if (!result.ok) {
          patchState(store, { errorCode: result.code });
          return false;
        }
        await load(store.caseId());
        return true;
      },
    };
  }),
);
