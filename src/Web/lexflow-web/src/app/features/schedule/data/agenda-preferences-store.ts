import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { ScheduleApi, type AgendaFilters } from './schedule-api';
import { validWindow } from './schedule-format';

interface SavedAgenda {
  name: string;
  filters: AgendaFilters;
}
const GUID = /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i;
function parseSaved(value: unknown): SavedAgenda[] {
  if (!Array.isArray(value)) return [];
  return value
    .slice(0, 10)
    .flatMap((raw: unknown) => {
      if (!raw || typeof raw !== 'object') return [];
      const { name, filters } = raw as Partial<SavedAgenda>;
      if (
        typeof name !== 'string' ||
        !name.trim() ||
        name.length > 50 ||
        !filters ||
        typeof filters.from !== 'string' ||
        typeof filters.until !== 'string' ||
        !validWindow(filters.from, filters.until) ||
        typeof filters.includeClosed !== 'boolean' ||
        ![filters.assigneeUserId, filters.teamId].every(
          (id) => id === null || (typeof id === 'string' && GUID.test(id)),
        )
      )
        return [];
      return [
        {
          name: name.trim(),
          filters: {
            from: filters.from,
            until: filters.until,
            includeClosed: filters.includeClosed,
            assigneeUserId: filters.assigneeUserId,
            teamId: filters.teamId,
          },
        },
      ];
    })
    .filter((item, index, all) => all.findIndex((row) => row.name === item.name) === index);
}
export const AgendaPreferencesStore = signalStore(
  withState({
    saved: [] as SavedAgenda[],
    ready: false,
    loading: false,
    busy: false,
    error: null as string | null,
  }),
  withMethods((store, api = inject(ScheduleApi)) => {
    let version = 0;
    const persist = async (saved: SavedAgenda[]) => {
      if (!store.ready() || store.busy()) return;
      const current = ++version;
      patchState(store, { busy: true, error: null });
      const result = await api.savePreference(saved);
      if (current !== version) return;
      patchState(
        store,
        result.ok ? { saved, busy: false } : { ready: false, busy: false, error: result.code },
      );
    };
    return {
      async load() {
        if (store.busy()) return;
        const current = ++version;
        patchState(store, { loading: true, ready: false, error: null });
        const result = await api.preference();
        if (current !== version) return;
        if (result.ok)
          patchState(store, { saved: parseSaved(result.value.value), ready: true, loading: false });
        else if (result.code === 'platform.preference_not_found')
          patchState(store, { saved: [], ready: true, loading: false });
        else patchState(store, { saved: [], loading: false, error: result.code });
      },
      async save(name: string, filters: AgendaFilters) {
        if (
          !store.ready() ||
          !name.trim() ||
          name.trim().length > 50 ||
          !validWindow(filters.from, filters.until)
        )
          return;
        const others = store.saved().filter((row) => row.name !== name.trim());
        if (others.length >= 10) {
          patchState(store, { error: 'schedule.filter_limit' });
          return;
        }
        await persist([...others, { name: name.trim(), filters }]);
      },
      remove(name: string) {
        return persist(store.saved().filter((row) => row.name !== name));
      },
    };
  }),
);
