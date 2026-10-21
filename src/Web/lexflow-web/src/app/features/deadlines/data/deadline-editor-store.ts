import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  CaseOptionsResponse,
  CaseSummary,
  ManualDeadlineRequest,
} from '../../../api/types.gen';
import { DeadlinesApi } from './deadlines-api';

export const DeadlineEditorStore = signalStore(
  withState({
    options: null as CaseOptionsResponse | null,
    optionsError: null as string | null,
    selectedCase: null as Pick<CaseSummary, 'id' | 'internalFolder' | 'cnjNumber'> | null,
    caseResults: [] as CaseSummary[],
    loading: false,
    busy: false,
    error: null as string | null,
  }),
  withMethods((store, api = inject(DeadlinesApi)) => {
    let contextVersion = 0;
    let searchVersion = 0;
    let optionsVersion = 0;

    function resetContext(): number {
      contextVersion++;
      searchVersion++;
      patchState(store, {
        selectedCase: null,
        caseResults: [],
        loading: false,
        busy: false,
        error: null,
      });
      return contextVersion;
    }

    async function loadOptions(): Promise<void> {
      const version = ++optionsVersion;
      patchState(store, { loading: true, optionsError: null });
      const result = await api.options();
      if (version !== optionsVersion) return;
      patchState(
        store,
        result.ok
          ? { options: result.value, optionsError: null, loading: false }
          : { optionsError: result.code, loading: false },
      );
    }

    async function changeCase(id: string | null): Promise<void> {
      const version = resetContext();
      if (!id) return;
      patchState(store, { loading: true });
      const result = await api.case(id);
      if (version !== contextVersion) return;
      patchState(
        store,
        result.ok
          ? {
              selectedCase: {
                id: result.value.id,
                internalFolder: result.value.internalFolder,
                cnjNumber: result.value.cnjNumber,
              },
              loading: false,
            }
          : { error: result.code, loading: false },
      );
    }

    async function searchCases(query: string): Promise<void> {
      if (store.busy()) return;
      const version = ++searchVersion;
      const context = contextVersion;
      const result = await api.cases(query);
      if (version !== searchVersion || context !== contextVersion) return;
      patchState(
        store,
        result.ok
          ? { caseResults: result.value.items, error: null }
          : { caseResults: [], error: result.code },
      );
    }

    async function selectCase(item: CaseSummary): Promise<void> {
      resetContext();
      patchState(store, { selectedCase: item });
    }

    async function save(caseId: string, body: ManualDeadlineRequest): Promise<string | null> {
      if (store.busy()) return null;
      const context = contextVersion;
      patchState(store, { busy: true, error: null });
      const result = await api.create(caseId, body);
      if (context !== contextVersion) return null;
      patchState(store, { busy: false, error: result.ok ? null : result.code });
      return result.ok ? result.value.id : null;
    }

    return { resetContext, loadOptions, changeCase, searchCases, selectCase, save };
  }),
);
