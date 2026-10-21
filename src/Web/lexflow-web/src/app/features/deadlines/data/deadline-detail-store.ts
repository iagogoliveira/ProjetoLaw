import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { DeadlineDetailResponse } from '../../../api/types.gen';
import { DeadlinesApi } from './deadlines-api';

export type DeadlineAction = 'start' | 'review' | 'complete' | 'miss';

export const DeadlineDetailStore = signalStore(
  withState({
    item: null as DeadlineDetailResponse | null,
    error: null as string | null,
    busy: false,
    loading: true,
  }),
  withMethods((store, api = inject(DeadlinesApi)) => {
    let version = 0;
    let currentId: string | null = null;

    async function load(id: string): Promise<void> {
      currentId = id;
      const request = ++version;
      patchState(store, { item: null, error: null, loading: true, busy: false });
      const result = await api.detail(id);
      if (request !== version) return;
      patchState(
        store,
        result.ok
          ? { item: result.value, error: null, loading: false }
          : { item: null, error: result.code, loading: false },
      );
    }

    async function act(action: DeadlineAction, evidence: string, reason: string): Promise<void> {
      const id = currentId;
      if (!id || store.busy()) return;
      const request = ++version;
      patchState(store, { busy: true, loading: true, item: null, error: null });
      const result =
        action === 'start'
          ? await api.start(id)
          : action === 'review'
            ? await api.review(id)
            : action === 'complete'
              ? await api.complete(id, evidence.trim())
              : await api.miss(id, reason.trim());
      if (request !== version || currentId !== id) return;
      if (result.ok) {
        patchState(store, { item: result.value, error: null, busy: false, loading: false });
        return;
      }
      const originalError = result.code;
      if (
        originalError === 'authorization.forbidden' ||
        originalError === 'auth.forbidden' ||
        originalError === 'organization.not_a_member' ||
        originalError === 'organization.mfa_required' ||
        originalError === 'scheduling.deadline_not_found'
      ) {
        patchState(store, { item: null, error: originalError, busy: false, loading: false });
        return;
      }
      const refreshed = await api.detail(id);
      if (request !== version || currentId !== id) return;
      patchState(store, {
        item: refreshed.ok ? refreshed.value : null,
        error: originalError,
        busy: false,
        loading: false,
      });
    }

    return { load, act };
  }),
);
