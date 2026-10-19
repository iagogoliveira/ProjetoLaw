import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type {
  AssignmentRole,
  CaseBatchAction,
  CaseBatchResult,
  CaseOptionsResponse,
  CaseSummary,
} from '../../../api/types.gen';
import { CaseFilters, CasesApi, NO_FILTERS } from './cases-api';

/** Colunas que a lista sabe mostrar. */
export const CASE_COLUMNS = [
  'folder',
  'cnj',
  'kind',
  'clients',
  'area',
  'phase',
  'status',
  'createdAt',
] as const;
export type CaseColumn = (typeof CASE_COLUMNS)[number];

export const DEFAULT_COLUMNS: CaseColumn[] = [
  'folder',
  'cnj',
  'clients',
  'area',
  'status',
  'createdAt',
];

/** Um filtro salvo pelo usuário: o nome e os filtros. */
export interface SavedFilter {
  name: string;
  filters: CaseFilters;
}

/** Preferência `cases.list` (decisão de 2026-10-18: no servidor, por usuário). */
export interface CasesListPreference {
  columns: CaseColumn[];
  saved: SavedFilter[];
}

export const PREFERENCE_KEY = 'cases.list';

/** Pedido de lote como a tela monta (os processos vêm da seleção). */
export interface BatchRequest {
  action: CaseBatchAction;
  fromUserId?: string | null;
  toUserId?: string | null;
  role?: AssignmentRole | null;
  phaseId?: string | null;
  note?: string | null;
}

interface CasesListState {
  items: CaseSummary[];
  nextCursor: string | null;
  filters: CaseFilters;
  options: CaseOptionsResponse | null;
  columns: CaseColumn[];
  saved: SavedFilter[];
  selected: string[];
  batchResult: CaseBatchResult | null;
  loading: boolean;
  busy: boolean;
  errorCode: string | null;
}

/**
 * Lista de processos (5E1): filtros (situação, tipo, área, filial, fase, responsável, equipe, pessoa e busca),
 * filtros salvos e colunas guardados como preferência do usuário, "carregar mais" pelo cursor e as operações em lote
 * sobre os selecionados. Só a resposta da última busca conta.
 */
export const CasesListStore = signalStore(
  withState<CasesListState>({
    items: [],
    nextCursor: null,
    filters: NO_FILTERS,
    options: null,
    columns: DEFAULT_COLUMNS,
    saved: [],
    selected: [],
    batchResult: null,
    loading: false,
    busy: false,
    errorCode: null,
  }),
  withComputed(({ items, selected }) => ({
    allSelected: computed(
      () => items().length > 0 && items().every((c) => selected().includes(c.id)),
    ),
  })),
  withMethods((store, api = inject(CasesApi)) => {
    let lastSearch = 0;

    async function search(filters: CaseFilters): Promise<void> {
      const id = ++lastSearch;
      patchState(store, { filters, loading: true, errorCode: null, selected: [] });
      const result = await api.list(filters, null);
      if (id !== lastSearch) {
        return;
      }

      patchState(
        store,
        result.ok
          ? { items: result.value.items, nextCursor: result.value.nextCursor, loading: false }
          : { items: [], nextCursor: null, loading: false, errorCode: result.code },
      );
    }

    async function init(): Promise<void> {
      const [options, preference] = await Promise.all([
        api.options(),
        api.preference(PREFERENCE_KEY),
      ]);
      const saved = preference.ok ? (preference.value.value as Partial<CasesListPreference>) : {};
      patchState(store, {
        options: options.ok ? options.value : null,
        columns: (saved.columns ?? DEFAULT_COLUMNS).filter((c) => CASE_COLUMNS.includes(c)),
        saved: saved.saved ?? [],
      });
      await search(NO_FILTERS);
    }

    async function loadMore(): Promise<void> {
      const cursor = store.nextCursor();
      if (!cursor || store.loading()) {
        return;
      }

      patchState(store, { loading: true });
      const result = await api.list(store.filters(), cursor);
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

    async function persist(columns: CaseColumn[], saved: SavedFilter[]): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.savePreference(PREFERENCE_KEY, { columns, saved });
      patchState(
        store,
        result.ok ? { busy: false, columns, saved } : { busy: false, errorCode: result.code },
      );
      return result.ok;
    }

    /** Guarda os filtros atuais com o nome (o mesmo nome substitui). */
    const saveFilter = (name: string) =>
      persist(store.columns(), [
        ...store.saved().filter((s) => s.name !== name.trim()),
        { name: name.trim(), filters: store.filters() },
      ]);

    const removeFilter = (name: string) =>
      persist(
        store.columns(),
        store.saved().filter((s) => s.name !== name),
      );

    const setColumns = (columns: CaseColumn[]) =>
      persist(
        CASE_COLUMNS.filter((c) => columns.includes(c)),
        store.saved(),
      );

    function toggle(id: string): void {
      const selected = store.selected();
      patchState(store, {
        selected: selected.includes(id) ? selected.filter((s) => s !== id) : [...selected, id],
      });
    }

    function toggleAll(): void {
      patchState(store, { selected: store.allSelected() ? [] : store.items().map((c) => c.id) });
    }

    /** Aplica a ação aos selecionados; a lista é recarregada e o resultado fica em `batchResult`. */
    async function runBatch(request: BatchRequest): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null, batchResult: null });
      const result = await api.batch({
        caseIds: store.selected(),
        action: request.action,
        fromUserId: request.fromUserId ?? null,
        toUserId: request.toUserId ?? null,
        role: request.role ?? null,
        phaseId: request.phaseId ?? null,
        note: request.note ?? null,
      });
      patchState(
        store,
        result.ok
          ? { busy: false, batchResult: result.value }
          : { busy: false, errorCode: result.code },
      );
      if (result.ok) {
        await search(store.filters());
      }
      return result.ok;
    }

    return {
      init,
      search,
      loadMore,
      saveFilter,
      removeFilter,
      setColumns,
      toggle,
      toggleAll,
      runBatch,
    };
  }),
);
