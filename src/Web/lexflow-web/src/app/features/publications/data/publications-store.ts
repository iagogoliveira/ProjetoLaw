import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  PublicationResponse,
  PublicationScreenOptionsResponse,
  PublicationSummary,
  PublicationTriageInput,
} from '../../../api/types.gen';
import { ApiResult } from '../../../core/data/api-result';
import { PENDING_FILTERS, PublicationFilters, PublicationsApi } from './publications-api';

export const PublicationsStore = signalStore(
  withState({
    items: [] as PublicationSummary[],
    nextCursor: null as string | null,
    selected: null as PublicationResponse | null,
    options: null as PublicationScreenOptionsResponse | null,
    filters: PENDING_FILTERS,
    loading: false,
    selecting: false,
    busy: false,
    errorCode: null as string | null,
    notice: false,
  }),
  withMethods((store, api = inject(PublicationsApi)) => {
    let searchVersion = 0;
    let selectionVersion = 0;
    let optionsVersion = 0;
    async function select(id: string | null): Promise<void> {
      const version = ++selectionVersion;
      patchState(store, {
        selected: null,
        selecting: !!id,
        errorCode: id ? null : store.errorCode(),
      });
      if (!id) return;
      const result = await api.detail(id);
      if (version !== selectionVersion) return;
      patchState(
        store,
        result.ok
          ? { selected: result.value, selecting: false }
          : { selecting: false, errorCode: result.code },
      );
    }
    async function search(filters: PublicationFilters): Promise<void> {
      const version = ++searchVersion;
      ++selectionVersion;
      patchState(store, {
        filters,
        loading: true,
        selecting: false,
        selected: null,
        items: [],
        nextCursor: null,
        errorCode: null,
        notice: false,
      });
      const result = await api.list(filters, null);
      if (version !== searchVersion) return;
      patchState(
        store,
        result.ok
          ? { items: result.value.items, nextCursor: result.value.nextCursor, loading: false }
          : { loading: false, errorCode: result.code },
      );
    }
    async function more(): Promise<void> {
      const cursor = store.nextCursor();
      if (!cursor || store.loading()) return;
      const version = searchVersion;
      patchState(store, { loading: true, errorCode: null });
      const result = await api.list(store.filters(), cursor);
      if (version !== searchVersion) return;
      patchState(
        store,
        result.ok
          ? {
              items: [...store.items(), ...result.value.items],
              nextCursor: result.value.nextCursor,
              loading: false,
            }
          : { loading: false, errorCode: result.code },
      );
    }
    async function options(term = ''): Promise<void> {
      const version = ++optionsVersion;
      const result = await api.options(term);
      if (version !== optionsVersion) return;
      patchState(store, result.ok ? { options: result.value } : { errorCode: result.code });
    }
    async function mutate(call: () => Promise<ApiResult<unknown>>): Promise<boolean> {
      if (store.busy()) return false;
      const context = ++searchVersion;
      const selection = selectionVersion;
      const id = store.selected()?.id;
      patchState(store, { busy: true, errorCode: null });
      const result = await call();
      if (context !== searchVersion || selection !== selectionVersion) {
        patchState(store, { busy: false });
        return result.ok;
      }
      if (!result.ok) {
        patchState(store, { busy: false, loading: false, errorCode: result.code });
        return false;
      }
      const page = await api.list(store.filters(), null);
      const detail =
        page.ok && id && page.value.items.some((p) => p.id === id) ? await api.detail(id) : null;
      if (context !== searchVersion || selection !== selectionVersion) {
        patchState(store, { busy: false });
        return true;
      }
      patchState(store, {
        busy: false,
        loading: false,
        notice: false,
        items: page.ok ? page.value.items : [],
        nextCursor: page.ok ? page.value.nextCursor : null,
        selected: detail?.ok ? detail.value : null,
        errorCode: !page.ok ? page.code : detail && !detail.ok ? detail.code : null,
      });
      return true;
    }
    return {
      search,
      more,
      select,
      loadOptions: options,
      news: () => patchState(store, { notice: true }),
      link: (id: string, caseId: string) => mutate(() => api.link(id, caseId)),
      assign: (id: string, userId: string | null) => mutate(() => api.assign(id, userId)),
      triage: (id: string, input: PublicationTriageInput) => mutate(() => api.triage(id, input)),
    };
  }),
);
