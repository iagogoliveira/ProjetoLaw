import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { ControlReportResponse } from '../../../api/types.gen';
import { ControlApi } from './control-api';

export interface ControlFilters {
  from: string;
  until: string;
  idleDays: number;
}
export interface SavedControlFilter {
  name: string;
  filters: ControlFilters;
}
interface State {
  report: ControlReportResponse | null;
  filters: ControlFilters | null;
  saved: SavedControlFilter[];
  savedReady: boolean;
  loading: boolean;
  busy: boolean;
  error: string | null;
  preferenceError: string | null;
}
export const CONTROL_PREFERENCE_KEY = 'control.filters';
const maxSaved = 10;
const datePattern = /^\d{4}-\d{2}-\d{2}$/;

function record(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
function validDate(value: unknown): value is string {
  if (typeof value !== 'string' || !datePattern.test(value)) return false;
  const date = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(date.valueOf()) && date.toISOString().slice(0, 10) === value;
}
function readFilters(value: unknown): ControlFilters | null {
  if (
    !record(value) ||
    !validDate(value['from']) ||
    !validDate(value['until']) ||
    !Number.isInteger(value['idleDays']) ||
    Number(value['idleDays']) < 1 ||
    Number(value['idleDays']) > 3650
  )
    return null;
  const days =
    (Date.parse(`${value['until']}T00:00:00Z`) - Date.parse(`${value['from']}T00:00:00Z`)) /
    86400000;
  return days > 0 && days <= 366
    ? { from: value['from'], until: value['until'], idleDays: Number(value['idleDays']) }
    : null;
}
export function readControlPreference(value: unknown): SavedControlFilter[] {
  if (!record(value) || !Array.isArray(value['saved'])) return [];
  const result: SavedControlFilter[] = [];
  for (const entry of value['saved']) {
    if (!record(entry) || typeof entry['name'] !== 'string') continue;
    const name = entry['name'].trim();
    const filters = readFilters(entry['filters']);
    if (!name || name.length > 50 || !filters || result.some((item) => item.name === name))
      continue;
    result.push({ name, filters });
    if (result.length === maxSaved) break;
  }
  return result;
}

export const ControlStore = signalStore(
  withState<State>({
    report: null,
    filters: null,
    saved: [],
    savedReady: false,
    loading: false,
    busy: false,
    error: null,
    preferenceError: null,
  }),
  withMethods((store, api = inject(ControlApi)) => {
    let reportVersion = 0;
    let preferenceVersion = 0;
    async function search(filters: ControlFilters) {
      const version = ++reportVersion;
      patchState(store, { filters, report: null, loading: true, error: null });
      const result = await api.report({
        from: filters.from,
        until: filters.until,
        idleDays: filters.idleDays,
        limit: 100,
      });
      if (version !== reportVersion) return;
      patchState(
        store,
        result.ok
          ? { report: result.value, loading: false }
          : { report: null, loading: false, error: result.code },
      );
    }
    async function loadSaved() {
      const version = ++preferenceVersion;
      patchState(store, { savedReady: false, preferenceError: null });
      const result = await api.preference();
      if (version !== preferenceVersion) return;
      if (!result.ok && result.code === 'platform.preference_not_found') {
        patchState(store, { saved: [], savedReady: true });
        return;
      }
      patchState(
        store,
        result.ok
          ? { saved: readControlPreference(result.value.value), savedReady: true }
          : { saved: [], savedReady: false, preferenceError: result.code },
      );
    }
    async function persist(saved: SavedControlFilter[]) {
      if (!store.savedReady() || store.busy() || saved.length > maxSaved) return false;
      const version = ++preferenceVersion;
      patchState(store, { busy: true, preferenceError: null });
      const result = await api.savePreference({ saved });
      if (version !== preferenceVersion) return false;
      patchState(
        store,
        result.ok
          ? { saved, busy: false }
          : { saved: [], savedReady: false, busy: false, preferenceError: result.code },
      );
      return result.ok;
    }
    async function save(name: string) {
      const normalized = name.trim();
      const filters = store.filters();
      if (!normalized || normalized.length > 50 || !filters) return false;
      const remaining = store.saved().filter((item) => item.name !== normalized);
      if (remaining.length >= maxSaved) {
        patchState(store, { preferenceError: 'control.filter_limit' });
        return false;
      }
      return persist([...remaining, { name: normalized, filters }]);
    }
    async function remove(name: string) {
      return persist(store.saved().filter((item) => item.name !== name));
    }
    return { search, loadSaved, save, remove };
  }),
);
