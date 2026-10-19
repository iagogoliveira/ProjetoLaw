import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  HearingInput,
  HearingResponse,
  PersonSummary,
  RescheduleHearingRequest,
} from '../../../api/types.gen';
import type { FieldErrors } from '../../../core/data/api-result';
import { CasesApi } from './cases-api';

export const HearingsStore = signalStore(
  withState({
    items: [] as HearingResponse[],
    people: [] as PersonSummary[],
    witnessNames: {} as Record<string, string | null>,
    busy: false,
    errorCode: null as string | null,
    fieldErrors: undefined as FieldErrors | undefined,
  }),
  withMethods((store, api = inject(CasesApi)) => {
    let caseId = '';
    let search = 0;
    const ordered = (items: HearingResponse[]) =>
      [...items].sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt));
    async function names(items: HearingResponse[]): Promise<void> {
      const ids = [...new Set(items.flatMap((h) => h.witnessPersonIds))];
      const results = await Promise.all(
        ids.map(async (id) => {
          const result = await api.person(id);
          return [id, result.ok ? result.value.name : null] as const;
        }),
      );
      patchState(store, {
        witnessNames: { ...store.witnessNames(), ...Object.fromEntries(results) },
      });
    }
    async function load(id: string): Promise<void> {
      caseId = id;
      const result = await api.hearings(id);
      patchState(
        store,
        result.ok ? { items: ordered(result.value), errorCode: null } : { errorCode: result.code },
      );
      if (result.ok) await names(result.value);
    }
    async function change(call: () => ReturnType<CasesApi['saveHearing']>): Promise<boolean> {
      if (store.busy()) return false;
      patchState(store, { busy: true, errorCode: null, fieldErrors: undefined });
      const result = await call();
      if (result.ok) {
        patchState(store, {
          items: ordered([result.value, ...store.items().filter((h) => h.id !== result.value.id)]),
        });
        await names([result.value]);
      }
      patchState(store, {
        busy: false,
        ...(!result.ok ? { errorCode: result.code, fieldErrors: result.errors } : {}),
      });
      return result.ok;
    }
    async function searchPeople(term: string): Promise<void> {
      const current = ++search;
      if (term.trim().length < 2) {
        patchState(store, { people: [] });
        return;
      }
      const result = await api.searchPeople(term.trim());
      if (current === search)
        patchState(
          store,
          result.ok
            ? {
                people: result.value.items,
                witnessNames: {
                  ...store.witnessNames(),
                  ...Object.fromEntries(result.value.items.map((p) => [p.id, p.name])),
                },
              }
            : { people: [], errorCode: result.code },
        );
    }
    return {
      load,
      searchPeople,
      save: (id: string | null, input: HearingInput) =>
        change(() => api.saveHearing(caseId, id, input)),
      reschedule: (id: string, input: RescheduleHearingRequest) =>
        change(() => api.rescheduleHearing(caseId, id, input)),
      end: (id: string, step: 'hold' | 'cancel', text: string) =>
        change(() => api.endHearing(caseId, id, step, text)),
    };
  }),
);
