import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type { CaseOptionsResponse, DeadlineListItem } from '../../../api/types.gen';
import { DeadlinesApi, type DeadlineFilters } from './deadlines-api';

export interface SavedDeadlineFilter {
  name: string;
  filters: DeadlineFilters;
}

export interface DeadlineListPreference {
  saved: SavedDeadlineFilter[];
}

export const DEADLINE_LIST_PREFERENCE_KEY = 'deadlines.list';
const MAX_SAVED_FILTERS = 10;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function isCivilDate(value: unknown): value is string {
  if (typeof value !== 'string' || !ISO_DATE.test(value)) return false;
  const date = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

interface DeadlinesListState {
  items: DeadlineListItem[];
  nextCursor: string | null;
  filters: DeadlineFilters | null;
  today: string;
  options: CaseOptionsResponse | null;
  saved: SavedDeadlineFilter[];
  savedReady: boolean;
  savedLoading: boolean;
  loading: boolean;
  busy: boolean;
  errorCode: string | null;
  optionsError: string | null;
  preferenceError: string | null;
}

export function readDeadlineListPreference(value: unknown): SavedDeadlineFilter[] {
  if (!isRecord(value) || !Array.isArray(value['saved'])) return [];
  const saved: SavedDeadlineFilter[] = [];
  for (const entry of value['saved']) {
    if (!isRecord(entry)) continue;
    const name = entry['name'];
    const filters = readSavedFilters(entry['filters']);
    if (typeof name !== 'string' || !name.trim() || name.trim().length > 50 || !filters) continue;
    if (saved.some((filter) => filter.name === name.trim())) continue;
    saved.push({ name: name.trim(), filters });
    if (saved.length === MAX_SAVED_FILTERS) break;
  }
  return saved;
}

function readSavedFilters(value: unknown): DeadlineFilters | null {
  if (!isRecord(value)) return null;
  const { from, until, assigneeUserId, teamId, includeClosed, caseId } = value;
  if (
    !isCivilDate(from) ||
    !isCivilDate(until) ||
    (assigneeUserId !== null && typeof assigneeUserId !== 'string') ||
    (teamId !== null && typeof teamId !== 'string') ||
    typeof includeClosed !== 'boolean' ||
    (caseId !== undefined && caseId !== null && typeof caseId !== 'string')
  )
    return null;
  const windowDays =
    (Date.parse(`${until}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000;
  if (windowDays < 1 || windowDays > 366) return null;
  if ([assigneeUserId, teamId, caseId].some((id) => typeof id === 'string' && !GUID.test(id)))
    return null;
  return { from, until, assigneeUserId, teamId, includeClosed, caseId: caseId ?? null };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export const DeadlinesListStore = signalStore(
  withState<DeadlinesListState>({
    items: [],
    nextCursor: null,
    filters: null,
    today: '',
    options: null,
    saved: [],
    savedReady: false,
    savedLoading: false,
    loading: false,
    busy: false,
    errorCode: null,
    optionsError: null,
    preferenceError: null,
  }),
  withComputed(({ items, loading, nextCursor }) => ({
    hasMore: computed(() => nextCursor() !== null && !loading()),
    isEmpty: computed(() => items().length === 0 && !loading()),
  })),
  withMethods((store, api = inject(DeadlinesApi)) => {
    let searchVersion = 0;
    let moreVersion = 0;
    let preferenceVersion = 0;

    async function search(filters: DeadlineFilters): Promise<void> {
      const request = ++searchVersion;
      moreVersion++;
      patchState(store, { filters, items: [], nextCursor: null, loading: true, errorCode: null });
      const result = await api.list(filters, null);
      if (request !== searchVersion) return;
      patchState(
        store,
        result.ok
          ? {
              items: result.value.items,
              nextCursor: result.value.nextCursor,
              today: result.value.today,
              loading: false,
            }
          : { items: [], nextCursor: null, loading: false, errorCode: result.code },
      );
    }

    async function loadMore(): Promise<void> {
      const cursor = store.nextCursor();
      const filters = store.filters();
      if (!cursor || !filters || store.loading()) return;
      const request = ++moreVersion;
      const parentSearchVersion = searchVersion;
      patchState(store, { loading: true, errorCode: null });
      const result = await api.list(filters, cursor);
      if (request !== moreVersion || parentSearchVersion !== searchVersion) return;
      patchState(
        store,
        result.ok
          ? {
              items: [...store.items(), ...result.value.items],
              nextCursor: result.value.nextCursor,
              today: result.value.today,
              loading: false,
            }
          : { items: [], nextCursor: null, loading: false, errorCode: result.code },
      );
    }

    async function loadOptions(): Promise<void> {
      const result = await api.options();
      patchState(
        store,
        result.ok
          ? { options: result.value, optionsError: null }
          : { options: null, optionsError: result.code },
      );
    }

    async function loadSaved(): Promise<void> {
      if (store.busy()) return;
      const request = ++preferenceVersion;
      patchState(store, { savedReady: false, savedLoading: true, preferenceError: null });
      const result = await api.preference();
      if (request !== preferenceVersion) return;
      if (!result.ok && result.code === 'platform.preference_not_found') {
        patchState(store, {
          saved: [],
          savedReady: true,
          savedLoading: false,
          preferenceError: null,
        });
        return;
      }
      patchState(
        store,
        result.ok
          ? {
              saved: readDeadlineListPreference(result.value.value),
              savedReady: true,
              savedLoading: false,
              preferenceError: null,
            }
          : { saved: [], savedReady: false, savedLoading: false, preferenceError: result.code },
      );
    }

    async function persist(saved: SavedDeadlineFilter[]): Promise<boolean> {
      if (store.busy() || !store.savedReady() || store.savedLoading()) return false;
      if (saved.length > MAX_SAVED_FILTERS) {
        patchState(store, { preferenceError: 'deadlines.filter_limit' });
        return false;
      }
      patchState(store, { busy: true, preferenceError: null });
      const request = ++preferenceVersion;
      const result = await api.savePreference({ saved });
      if (request !== preferenceVersion) return false;
      patchState(
        store,
        result.ok
          ? { busy: false, saved, preferenceError: null }
          : { busy: false, savedReady: false, preferenceError: result.code },
      );
      return result.ok;
    }

    async function saveFilter(name: string): Promise<boolean> {
      const normalizedName = name.trim();
      const filters = store.filters();
      if (!normalizedName || normalizedName.length > 50 || !filters) return false;
      const saved = store.saved().filter((filter) => filter.name !== normalizedName);
      if (saved.length >= MAX_SAVED_FILTERS) {
        patchState(store, { preferenceError: 'deadlines.filter_limit' });
        return false;
      }
      return persist([...saved, { name: normalizedName, filters }]);
    }

    async function removeFilter(name: string): Promise<boolean> {
      return persist(store.saved().filter((filter) => filter.name !== name));
    }

    return { search, loadMore, loadOptions, loadSaved, saveFilter, removeFilter };
  }),
);
