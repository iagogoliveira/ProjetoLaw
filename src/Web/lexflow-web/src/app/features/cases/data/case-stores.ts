import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  CaseInput,
  CaseKind,
  CaseOptionsResponse,
  CaseResponse,
  CaseRestrictionsResponse,
  CaseSummary,
  ClosingInput,
  CnjInfoResponse,
  NumberingResponse,
  PersonKind,
  PersonResponse,
  PersonRoleKind,
  PersonSummary,
  RelationKind,
  RestrictionEntry,
  TimelineEntry,
} from '../../../api/types.gen';
import type { FieldErrors } from '../../../core/data/api-result';
import { CasesApi, NO_FILTERS } from './cases-api';

interface CaseEditorState {
  options: CaseOptionsResponse | null;
  legalCase: CaseResponse | null;
  cnj: CnjInfoResponse | null;
  people: PersonSummary[];
  busy: boolean;
  errorCode: string | null;
  fieldErrors: FieldErrors | undefined;
}

/**
 * Assistente de cadastro e edição do processo: as opções do escritório, a consulta do CNJ (tribunal e ano), a busca e
 * o cadastro rápido de pessoas para as partes, e a gravação. Os erros por campo da API ficam em `fieldErrors`.
 */
export const CaseEditorStore = signalStore(
  withState<CaseEditorState>({
    options: null,
    legalCase: null,
    cnj: null,
    people: [],
    busy: false,
    errorCode: null,
    fieldErrors: undefined,
  }),
  withMethods((store, api = inject(CasesApi)) => {
    let lastPeople = 0;

    async function load(id: string | null): Promise<void> {
      const [options, legalCase] = await Promise.all([
        api.options(),
        id ? api.get(id) : Promise.resolve(null),
      ]);
      patchState(store, {
        options: options.ok ? options.value : null,
        legalCase: legalCase?.ok ? legalCase.value : null,
        errorCode: legalCase && !legalCase.ok ? legalCase.code : null,
      });
    }

    /** Confere o CNJ e traz o tribunal; nulo com o motivo em `errorCode`. */
    async function lookupCnj(number: string): Promise<CnjInfoResponse | null> {
      patchState(store, { busy: true, errorCode: null, cnj: null });
      const result = await api.lookupCnj(number);
      patchState(
        store,
        result.ok ? { busy: false, cnj: result.value } : { busy: false, errorCode: result.code },
      );
      return result.ok ? result.value : null;
    }

    async function searchPeople(term: string): Promise<void> {
      const id = ++lastPeople;
      if (term.trim().length < 2) {
        patchState(store, { people: [] });
        return;
      }

      const result = await api.searchPeople(term.trim());
      if (id === lastPeople) {
        patchState(store, { people: result.ok ? result.value.items : [] });
      }
    }

    /** Cadastro rápido de uma pessoa (nome, documento e papel) para usar como parte. */
    async function createPerson(
      kind: PersonKind,
      name: string,
      document: string | null,
      role: PersonRoleKind,
    ): Promise<PersonResponse | null> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.createPerson(kind, name, document, role);
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      return result.ok ? result.value : null;
    }

    /** Cria (com o tipo) ou atualiza o processo; devolve o gravado, ou nulo com os erros no estado. */
    async function save(kind: CaseKind, input: CaseInput): Promise<CaseResponse | null> {
      patchState(store, { busy: true, errorCode: null, fieldErrors: undefined });
      const existing = store.legalCase();
      const result = existing
        ? await api.update(existing.id, input)
        : await api.create(kind, input);
      patchState(
        store,
        result.ok
          ? { busy: false, legalCase: result.value }
          : { busy: false, errorCode: result.code, fieldErrors: result.errors },
      );
      return result.ok ? result.value : null;
    }

    return { load, lookupCnj, searchPeople, createPerson, save };
  }),
);

interface CaseDetailState {
  legalCase: CaseResponse | null;
  options: CaseOptionsResponse | null;
  restrictions: CaseRestrictionsResponse | null;
  timeline: TimelineEntry[];
  timelineCursor: string | null;
  timelineKinds: string[];
  candidates: CaseSummary[];
  busy: boolean;
  errorCode: string | null;
  done: 'deleted' | null;
}

/**
 * Detalhe do processo e as ações: encerrar, reabrir, suspender, reativar e excluir; a hierarquia (ligar e desligar);
 * as listas do caso sigiloso; e a linha do tempo com filtro por tipo e "carregar mais".
 */
export const CaseDetailStore = signalStore(
  withState<CaseDetailState>({
    legalCase: null,
    options: null,
    restrictions: null,
    timeline: [],
    timelineCursor: null,
    timelineKinds: [],
    candidates: [],
    busy: false,
    errorCode: null,
    done: null,
  }),
  withMethods((store, api = inject(CasesApi)) => {
    async function load(id: string): Promise<void> {
      const [legalCase, options] = await Promise.all([api.get(id), api.options()]);
      patchState(store, {
        legalCase: legalCase.ok ? legalCase.value : null,
        options: options.ok ? options.value : null,
        errorCode: legalCase.ok ? null : legalCase.code,
      });
    }

    async function change(call: () => ReturnType<CasesApi['get']>): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await call();
      patchState(
        store,
        result.ok
          ? { busy: false, legalCase: result.value }
          : { busy: false, errorCode: result.code },
      );
      return result.ok;
    }

    const id = () => store.legalCase()!.id;
    const close = (closing: ClosingInput) => change(() => api.close(id(), closing));
    const reopen = () => change(() => api.reopen(id()));
    const setStatus = (status: 'active' | 'suspended') => change(() => api.setStatus(id(), status));
    const link = (childCaseId: string, kind: RelationKind) =>
      change(() => api.link(id(), childCaseId, kind));

    async function unlink(relationId: string): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.unlink(id(), relationId);
      if (!result.ok) {
        patchState(store, { busy: false, errorCode: result.code });
        return false;
      }

      await load(id());
      patchState(store, { busy: false });
      return true;
    }

    async function remove(): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.remove(id());
      patchState(
        store,
        result.ok ? { busy: false, done: 'deleted' } : { busy: false, errorCode: result.code },
      );
      return result.ok;
    }

    /** Processos para ligar na hierarquia (busca por pasta ou CNJ, sem o próprio). */
    async function findCandidates(term: string): Promise<void> {
      if (term.trim().length < 2) {
        patchState(store, { candidates: [] });
        return;
      }

      const result = await api.list({ ...NO_FILTERS, q: term.trim() }, null);
      patchState(store, {
        candidates: result.ok ? result.value.items.filter((c) => c.id !== id()) : [],
      });
    }

    async function loadRestrictions(): Promise<void> {
      const result = await api.restrictions(id());
      patchState(store, {
        restrictions: result.ok ? result.value : null,
        errorCode: result.ok ? null : result.code,
      });
    }

    async function saveRestrictions(
      allowed: RestrictionEntry[],
      blocked: RestrictionEntry[],
    ): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.setRestrictions(id(), allowed, blocked);
      patchState(
        store,
        result.ok
          ? { busy: false, restrictions: result.value }
          : { busy: false, errorCode: result.code },
      );
      if (result.ok) {
        await load(id());
      }
      return result.ok;
    }

    async function loadTimeline(kinds: string[]): Promise<void> {
      patchState(store, { timelineKinds: kinds });
      const result = await api.timeline(id(), kinds, null);
      patchState(
        store,
        result.ok
          ? { timeline: result.value.items, timelineCursor: result.value.nextCursor }
          : { timeline: [], timelineCursor: null, errorCode: result.code },
      );
    }

    async function moreTimeline(): Promise<void> {
      const cursor = store.timelineCursor();
      if (!cursor) {
        return;
      }

      const result = await api.timeline(id(), store.timelineKinds(), cursor);
      patchState(
        store,
        result.ok
          ? {
              timeline: [...store.timeline(), ...result.value.items],
              timelineCursor: result.value.nextCursor,
            }
          : { errorCode: result.code },
      );
    }

    return {
      load,
      close,
      reopen,
      setStatus,
      link,
      unlink,
      remove,
      findCandidates,
      loadRestrictions,
      saveRestrictions,
      loadTimeline,
      moreTimeline,
    };
  }),
);

interface NumberingState {
  numbering: NumberingResponse | null;
  options: CaseOptionsResponse | null;
  busy: boolean;
  errorCode: string | null;
  saved: boolean;
}

/** Numeração interna dos processos (configurações): o formato e as siglas das áreas. */
export const NumberingStore = signalStore(
  withState<NumberingState>({
    numbering: null,
    options: null,
    busy: false,
    errorCode: null,
    saved: false,
  }),
  withMethods((store, api = inject(CasesApi)) => {
    async function load(): Promise<void> {
      const [numbering, options] = await Promise.all([api.numbering(), api.options()]);
      patchState(store, {
        numbering: numbering.ok ? numbering.value : null,
        options: options.ok ? options.value : null,
        errorCode: numbering.ok ? null : numbering.code,
      });
    }

    async function save(format: string, areaCodes: Record<string, string>): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null, saved: false });
      const result = await api.configureNumbering(format, areaCodes);
      patchState(
        store,
        result.ok
          ? { busy: false, numbering: result.value, saved: true }
          : { busy: false, errorCode: result.code },
      );
      return result.ok;
    }

    return { load, save };
  }),
);
