import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type { TaskDetailResponse, TaskPriority, TaskState } from '../../../api/types.gen';
import { TasksApi, type TaskFilters } from './tasks-api';

export interface SavedTaskFilter {
  name: string;
  filters: TaskFilters;
}
export const TASK_LIST_PREFERENCE_KEY = 'tasks.list';
const STATES: TaskState[] = ['pending', 'inProgress', 'completed', 'cancelled'];
const PRIORITIES: TaskPriority[] = ['low', 'normal', 'high', 'urgent'];
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const EMPTY_FILTERS: TaskFilters = {
  status: null,
  priority: null,
  assigneeUserId: null,
  teamId: null,
  caseId: null,
};

function record(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
export function readSavedTaskFilters(value: unknown): SavedTaskFilter[] {
  if (!record(value) || !Array.isArray(value['saved'])) return [];
  const saved: SavedTaskFilter[] = [];
  for (const entry of value['saved']) {
    if (!record(entry) || typeof entry['name'] !== 'string' || !record(entry['filters'])) continue;
    const name = entry['name'].trim();
    const f = entry['filters'];
    const status = f['status'];
    const priority = f['priority'];
    const assigneeUserId = f['assigneeUserId'];
    const teamId = f['teamId'];
    const caseId = f['caseId'];
    if (
      !name ||
      name.length > 50 ||
      (status !== null && !STATES.includes(status as TaskState)) ||
      (priority !== null && !PRIORITIES.includes(priority as TaskPriority)) ||
      [assigneeUserId, teamId, caseId].some(
        (id) => id !== null && (typeof id !== 'string' || !GUID.test(id)),
      ) ||
      saved.some((item) => item.name === name)
    )
      continue;
    saved.push({
      name,
      filters: {
        status: status as TaskState | null,
        priority: priority as TaskPriority | null,
        assigneeUserId: assigneeUserId as string | null,
        teamId: teamId as string | null,
        caseId: caseId as string | null,
      },
    });
    if (saved.length === 10) break;
  }
  return saved;
}

interface TasksState {
  items: TaskDetailResponse[];
  nextCursor: string | null;
  filters: TaskFilters;
  loading: boolean;
  busy: boolean;
  errorCode: string | null;
  options: {
    members: { userId: string; name: string }[];
    teams: { id: string; name: string }[];
  } | null;
  optionsError: string | null;
  saved: SavedTaskFilter[];
  savedReady: boolean;
  savedLoading: boolean;
  preferenceError: string | null;
  caseResults: { id: string; label: string }[];
  caseSearchLoading: boolean;
  redistribution:
    { id: string; title: string; changed: boolean; errorCode: string | null }[] | null;
}
export const TasksStore = signalStore(
  withState<TasksState>({
    items: [],
    nextCursor: null,
    filters: EMPTY_FILTERS,
    loading: false,
    busy: false,
    errorCode: null,
    options: null,
    optionsError: null,
    saved: [],
    savedReady: false,
    savedLoading: false,
    preferenceError: null,
    caseResults: [],
    caseSearchLoading: false,
    redistribution: null,
  }),
  withComputed(({ items, loading, nextCursor }) => ({
    hasMore: computed(() => nextCursor() !== null),
    canLoadMore: computed(() => nextCursor() !== null && !loading()),
    isEmpty: computed(() => items().length === 0 && !loading()),
  })),
  withMethods((store, api = inject(TasksApi)) => {
    let listVersion = 0,
      preferenceVersion = 0,
      caseVersion = 0;
    async function search(filters: TaskFilters): Promise<void> {
      const version = ++listVersion;
      patchState(store, {
        filters,
        items: [],
        nextCursor: null,
        loading: true,
        busy: false,
        errorCode: null,
        redistribution: null,
      });
      const result = await api.list(filters, null);
      if (version !== listVersion) return;
      patchState(
        store,
        result.ok
          ? { items: result.value.items, nextCursor: result.value.nextCursor, loading: false }
          : { items: [], nextCursor: null, loading: false, errorCode: result.code },
      );
    }
    async function loadMore(): Promise<void> {
      const after = store.nextCursor();
      if (!after || store.loading()) return;
      const version = listVersion,
        filters = store.filters();
      patchState(store, { loading: true, errorCode: null });
      const result = await api.list(filters, after);
      if (version !== listVersion) return;
      patchState(
        store,
        result.ok
          ? {
              items: [...store.items(), ...result.value.items],
              nextCursor: result.value.nextCursor,
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
      const version = ++preferenceVersion;
      patchState(store, { savedLoading: true, savedReady: false, preferenceError: null });
      const result = await api.preference();
      if (version !== preferenceVersion) return;
      if (!result.ok && result.code === 'platform.preference_not_found') {
        patchState(store, { saved: [], savedReady: true, savedLoading: false });
        return;
      }
      patchState(
        store,
        result.ok
          ? {
              saved: readSavedTaskFilters(result.value.value),
              savedReady: true,
              savedLoading: false,
            }
          : { savedReady: false, savedLoading: false, preferenceError: result.code },
      );
    }
    async function persist(saved: SavedTaskFilter[]): Promise<boolean> {
      if (store.busy() || !store.savedReady() || store.savedLoading()) return false;
      if (saved.length > 10) {
        patchState(store, { preferenceError: 'tasks.filterLimit' });
        return false;
      }
      const version = ++preferenceVersion;
      patchState(store, { busy: true, preferenceError: null });
      const result = await api.savePreference({ saved });
      if (version !== preferenceVersion) return false;
      patchState(
        store,
        result.ok
          ? { saved, busy: false }
          : { busy: false, savedReady: false, preferenceError: result.code },
      );
      return result.ok;
    }
    async function searchCases(query: string): Promise<void> {
      const version = ++caseVersion;
      if (!query.trim()) {
        patchState(store, { caseResults: [] });
        return;
      }
      patchState(store, { caseSearchLoading: true });
      const result = await api.cases(query.trim());
      if (version !== caseVersion) return;
      patchState(
        store,
        result.ok
          ? {
              caseResults: result.value.items.map((c) => ({
                id: c.id,
                label: c.cnjNumber ?? c.internalFolder,
              })),
              caseSearchLoading: false,
            }
          : { caseResults: [], caseSearchLoading: false },
      );
    }
    async function resolveCase(id: string): Promise<void> {
      const version = ++caseVersion;
      const result = await api.caseDetail(id);
      if (version !== caseVersion) return;
      patchState(
        store,
        result.ok
          ? {
              caseResults: [{ id, label: result.value.cnjNumber ?? result.value.internalFolder }],
            }
          : { caseResults: [] },
      );
    }
    async function mutate(
      id: string,
      action: (id: string) => Promise<{ ok: boolean; value?: TaskDetailResponse; code?: string }>,
    ): Promise<boolean> {
      if (store.busy()) return false;
      patchState(store, { busy: true, errorCode: null });
      const version = listVersion;
      const result = await action(id);
      if (version !== listVersion) return false;
      if (!result.ok) {
        const errorCode = result.code ?? 'http.error';
        patchState(store, { busy: false, errorCode });
        const reload = search(store.filters());
        const reloadVersion = listVersion;
        await reload;
        if (reloadVersion === listVersion) patchState(store, { errorCode });
        return false;
      }
      const reload = search(store.filters());
      const reloadVersion = listVersion;
      await reload;
      if (reloadVersion === listVersion) patchState(store, { busy: false });
      return true;
    }
    async function changeState(id: string, status: TaskState): Promise<boolean> {
      return mutate(id, (taskId) => api.state(taskId, status));
    }
    async function redistribute(ids: string[], assigneeUserId: string): Promise<void> {
      if (store.busy() || store.loading() || !ids.length || ids.length > 1000) return;
      const request = listVersion;
      const titles = new Map(store.items().map((item) => [item.id, item.title]));
      patchState(store, { busy: true, redistribution: null, errorCode: null });
      const result = await api.redistribute(ids, assigneeUserId);
      if (request !== listVersion) return;
      if (!result.ok) {
        patchState(store, { busy: false, errorCode: result.code });
        return;
      }
      const reload = search(store.filters());
      const reloadVersion = listVersion;
      await reload;
      if (reloadVersion === listVersion)
        patchState(store, {
          busy: false,
          redistribution: result.value.items.map((item) => ({
            ...item,
            title: titles.get(item.id) ?? '',
          })),
        });
    }
    async function saveFilter(name: string): Promise<boolean> {
      const normalized = name.trim();
      if (!normalized || normalized.length > 50) return false;
      const saved = store.saved().filter((x) => x.name !== normalized);
      if (saved.length >= 10) {
        patchState(store, { preferenceError: 'tasks.filterLimit' });
        return false;
      }
      return persist([...saved, { name: normalized, filters: store.filters() }]);
    }
    return {
      search,
      loadMore,
      loadOptions,
      loadSaved,
      searchCases,
      resolveCase,
      changeState,
      redistribute,
      saveFilter,
      removeFilter: (name: string) => persist(store.saved().filter((x) => x.name !== name)),
    };
  }),
);
