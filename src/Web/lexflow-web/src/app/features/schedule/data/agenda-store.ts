import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  AgendaItem,
  RedistributionItem,
  SchedulingOptionsResponse,
} from '../../../api/types.gen';
import { ScheduleApi, type AgendaFilters } from './schedule-api';
import { validWindow } from './schedule-format';

export const AgendaStore = signalStore(
  withState({
    items: [] as AgendaItem[],
    cursor: null as string | null,
    filters: null as AgendaFilters | null,
    loading: false,
    busy: false,
    error: null as string | null,
    options: null as SchedulingOptionsResponse | null,
    optionsError: null as string | null,
    redistribution: [] as (RedistributionItem & { title: string })[],
  }),
  withMethods((store, api = inject(ScheduleApi)) => {
    let version = 0;
    const search = async (filters: AgendaFilters, more = false) => {
      if (more && (store.loading() || !store.cursor())) return;
      const current = ++version;
      if (!validWindow(filters.from, filters.until)) {
        patchState(store, {
          items: [],
          cursor: null,
          loading: false,
          error: 'scheduling.agenda_invalid',
        });
        return;
      }
      const cursor = more ? store.cursor() : null;
      patchState(store, {
        filters,
        loading: true,
        error: null,
        ...(more ? {} : { items: [], cursor: null }),
      });
      const result = await api.agenda(filters, cursor);
      if (current !== version) return;
      if (result.ok) {
        const rows = [...(more ? store.items() : []), ...result.value.items];
        patchState(store, {
          items: [...new Map(rows.map((row) => [`${row.kind}:${row.id}`, row])).values()],
          cursor: result.value.nextCursor,
          loading: false,
        });
      } else patchState(store, { items: [], cursor: null, loading: false, error: result.code });
    };
    return {
      search,
      async optionsLoad() {
        const result = await api.options();
        patchState(
          store,
          result.ok
            ? { options: result.value, optionsError: null }
            : { options: null, optionsError: result.code },
        );
      },
      async redistribute(items: AgendaItem[], assigneeUserId: string) {
        if (store.busy() || !items.length || items.length > 1000) return;
        patchState(store, { busy: true, error: null, redistribution: [] });
        const results: (RedistributionItem & { title: string })[] = [];
        for (const kind of ['deadline', 'task', 'event'] as const) {
          const group = items.filter((item) => item.kind === kind);
          if (!group.length) continue;
          const result = await api.redistribute(kind, {
            ids: group.map((item) => item.id),
            assigneeUserId,
          });
          for (const item of group) {
            const outcome = result.ok ? result.value.items.find((row) => row.id === item.id) : null;
            results.push({
              id: item.id,
              changed: outcome?.changed ?? false,
              errorCode: outcome?.errorCode ?? (result.ok ? 'http.error' : result.code),
              title: item.title,
            });
          }
        }
        patchState(store, { redistribution: results, busy: false });
        const filters = store.filters();
        if (filters) await search(filters);
      },
    };
  }),
);
