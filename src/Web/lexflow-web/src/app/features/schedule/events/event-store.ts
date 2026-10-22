import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  CalendarEventInput,
  CalendarEventResponse,
  CaseSummary,
  SchedulingOptionsResponse,
} from '../../../api/types.gen';
import { ScheduleApi } from '../data/schedule-api';

interface State {
  item: CalendarEventResponse | null;
  caseLabel: string | null;
  cases: CaseSummary[];
  options: SchedulingOptionsResponse | null;
  loading: boolean;
  saving: boolean;
  canceling: boolean;
  searchingCases: boolean;
  error: string | null;
  optionsError: string | null;
  casesError: string | null;
  itemRevision: number;
}

export const EventStore = signalStore(
  withState<State>({
    item: null,
    caseLabel: null,
    cases: [],
    options: null,
    loading: false,
    saving: false,
    canceling: false,
    searchingCases: false,
    error: null,
    optionsError: null,
    casesError: null,
    itemRevision: 0,
  }),
  withMethods((store, api = inject(ScheduleApi)) => {
    let resourceVersion = 0;
    let caseSearchVersion = 0;
    let currentId: string | null = null;

    async function loadOptions(): Promise<void> {
      const result = await api.options();
      if (result.ok) patchState(store, { options: result.value, optionsError: null });
      else patchState(store, { options: null, optionsError: result.code });
    }

    async function load(id: string, canReadCases: boolean): Promise<void> {
      const version = ++resourceVersion;
      currentId = id;
      caseSearchVersion++;
      patchState(store, {
        item: null,
        caseLabel: null,
        cases: [],
        loading: true,
        saving: false,
        canceling: false,
        error: null,
        casesError: null,
      });
      const result = await api.event(id);
      if (version !== resourceVersion || currentId !== id) return;
      if (!result.ok) {
        patchState(store, { item: null, loading: false, error: result.code });
        return;
      }
      patchState(store, {
        item: result.value,
        loading: false,
        caseLabel: result.value.caseId ? 'linked' : null,
        itemRevision: store.itemRevision() + 1,
      });
      if (result.value.caseId && canReadCases) {
        const caseResult = await api.case(result.value.caseId);
        if (version !== resourceVersion || currentId !== id) return;
        patchState(store, {
          caseLabel: caseResult.ok ? caseResult.value.internalFolder : 'linked',
        });
      }
    }

    async function searchCases(query: string, allowed: boolean): Promise<void> {
      const version = ++caseSearchVersion;
      if (!allowed || !query.trim()) {
        patchState(store, { cases: [], searchingCases: false, casesError: null });
        return;
      }
      patchState(store, { cases: [], searchingCases: true, casesError: null });
      const result = await api.cases(query.trim());
      if (version !== caseSearchVersion) return;
      patchState(
        store,
        result.ok
          ? { cases: result.value.items, searchingCases: false }
          : { cases: [], searchingCases: false, casesError: result.code },
      );
    }

    function setCaseLabel(caseLabel: string | null): void {
      patchState(store, { caseLabel });
    }

    async function create(input: CalendarEventInput): Promise<CalendarEventResponse | null> {
      if (store.saving()) return null;
      const version = ++resourceVersion;
      currentId = null;
      patchState(store, { saving: true, error: null });
      const result = await api.create(input);
      if (version !== resourceVersion || currentId !== null) return null;
      patchState(
        store,
        result.ok
          ? { item: result.value, saving: false, itemRevision: store.itemRevision() + 1 }
          : { item: null, saving: false, error: result.code },
      );
      return result.ok ? result.value : null;
    }

    async function edit(id: string, input: CalendarEventInput): Promise<boolean> {
      if (store.saving() || store.canceling() || currentId !== id) return false;
      const version = ++resourceVersion;
      patchState(store, { saving: true, error: null });
      const result = await api.edit(id, input);
      if (version !== resourceVersion || currentId !== id) return false;
      if (result.ok)
        patchState(store, {
          item: result.value,
          saving: false,
          error: null,
          itemRevision: store.itemRevision() + 1,
        });
      else
        patchState(store, {
          item: isMissingOrDenied(result.code) ? null : store.item(),
          saving: false,
          error: result.code,
        });
      return result.ok;
    }

    async function cancel(id: string): Promise<boolean> {
      if (store.saving() || store.canceling() || currentId !== id) return false;
      const version = ++resourceVersion;
      patchState(store, { canceling: true, error: null });
      const result = await api.cancel(id);
      if (version !== resourceVersion || currentId !== id) return false;
      if (result.ok)
        patchState(store, {
          item: result.value,
          canceling: false,
          error: null,
          itemRevision: store.itemRevision() + 1,
        });
      else
        patchState(store, {
          item: isMissingOrDenied(result.code) ? null : store.item(),
          canceling: false,
          error: result.code,
        });
      return result.ok;
    }

    function invalidate(): void {
      resourceVersion++;
      caseSearchVersion++;
      currentId = null;
      patchState(store, {
        item: null,
        caseLabel: null,
        cases: [],
        loading: false,
        saving: false,
        canceling: false,
        error: null,
        casesError: null,
      });
    }

    return { load, loadOptions, searchCases, setCaseLabel, create, edit, cancel, invalidate };
  }),
);

function isMissingOrDenied(code: string): boolean {
  return [
    'authorization.forbidden',
    'auth.forbidden',
    'organization.not_a_member',
    'organization.mfa_required',
    'scheduling.event_not_found',
  ].includes(code);
}
