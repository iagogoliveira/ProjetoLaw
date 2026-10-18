import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  CompanyRegistration,
  CreatePersonRequest,
  CustomFieldResponse,
  EconomicGroupResponse,
  LookupTableResponse,
  PersonKind,
  PersonInput,
  PersonResponse,
  PersonRoleKind,
  PersonSummary,
  SimilarPerson,
} from '../../../api/types.gen';
import type { FieldErrors } from '../../../core/data/api-result';
import { CrmApi } from './crm-api';

interface PeopleState {
  items: PersonSummary[];
  nextCursor: string | null;
  term: string;
  role: PersonRoleKind | null;
  loading: boolean;
  errorCode: string | null;
}

/**
 * Lista de pessoas (RF-06): ordem alfabética com "carregar mais", ou busca sem acentos por nome aproximado ou
 * documento (a busca devolve os mais parecidos, sem cursor). Só a resposta da última busca conta.
 */
export const PeopleStore = signalStore(
  withState<PeopleState>({
    items: [],
    nextCursor: null,
    term: '',
    role: null,
    loading: false,
    errorCode: null,
  }),
  withMethods((store, api = inject(CrmApi)) => {
    let lastSearch = 0;

    async function search(term: string, role: PersonRoleKind | null): Promise<void> {
      const id = ++lastSearch;
      patchState(store, { term, role, loading: true, errorCode: null });
      const result = await api.people(term.trim() || null, role, null);
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

    async function loadMore(): Promise<void> {
      const cursor = store.nextCursor();
      if (!cursor || store.loading()) {
        return;
      }

      patchState(store, { loading: true });
      const result = await api.people(store.term().trim() || null, store.role(), cursor);
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

    return { search, loadMore };
  }),
);

interface PersonEditorState {
  person: PersonResponse | null;
  groups: EconomicGroupResponse[];
  fields: CustomFieldResponse[];
  tables: LookupTableResponse[];
  similar: SimilarPerson[];
  busy: boolean;
  errorCode: string | null;
  fieldErrors: FieldErrors | undefined;
}

/**
 * Cadastro e edição de uma pessoa: grupos econômicos, campos customizados de pessoas, possíveis duplicatas enquanto
 * o nome e o documento são digitados e a consulta de CNPJ. Os erros por campo da API ficam em `fieldErrors`.
 */
export const PersonEditorStore = signalStore(
  withState<PersonEditorState>({
    person: null,
    groups: [],
    fields: [],
    tables: [],
    similar: [],
    busy: false,
    errorCode: null,
    fieldErrors: undefined,
  }),
  withMethods((store, api = inject(CrmApi)) => {
    let lastSimilar = 0;

    async function load(id: string | null): Promise<void> {
      const [groups, fields, tables, person] = await Promise.all([
        api.economicGroups(),
        api.customFields(),
        api.lookupTables(),
        id ? api.person(id) : Promise.resolve(null),
      ]);
      patchState(store, {
        groups: groups.ok ? groups.value : [],
        fields: fields.ok ? fields.value : [],
        tables: tables.ok ? tables.value : [],
        person: person?.ok ? person.value : null,
        errorCode: person && !person.ok ? person.code : null,
      });
    }

    async function findSimilar(name: string, document: string): Promise<void> {
      const id = ++lastSimilar;
      if (name.trim().length < 3 && document.trim().length === 0) {
        patchState(store, { similar: [] });
        return;
      }

      const result = await api.similar(
        name.trim() || null,
        document.trim() || null,
        store.person()?.id ?? null,
      );
      if (id === lastSimilar) {
        patchState(store, { similar: result.ok ? result.value : [] });
      }
    }

    /** Dados da empresa na Receita Federal, ou nulo (o motivo fica em `errorCode`). */
    async function lookupCnpj(cnpj: string): Promise<CompanyRegistration | null> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.lookupCnpj(cnpj);
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      return result.ok ? result.value : null;
    }

    async function createGroup(name: string): Promise<EconomicGroupResponse | null> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.createEconomicGroup(name);
      patchState(
        store,
        result.ok
          ? { busy: false, groups: [...store.groups(), result.value] }
          : { busy: false, errorCode: result.code },
      );
      return result.ok ? result.value : null;
    }

    /** Cria (com o tipo) ou atualiza o cadastro; devolve a pessoa gravada, ou nulo com os erros no estado. */
    async function save(kind: PersonKind, input: PersonInput): Promise<PersonResponse | null> {
      patchState(store, { busy: true, errorCode: null, fieldErrors: undefined });
      const existing = store.person();
      const result = existing
        ? await api.updatePerson(existing.id, input)
        : await api.createPerson({ kind, ...input } satisfies CreatePersonRequest);
      patchState(
        store,
        result.ok
          ? { busy: false, person: result.value }
          : { busy: false, errorCode: result.code, fieldErrors: result.errors },
      );
      return result.ok ? result.value : null;
    }

    return { load, findSimilar, lookupCnpj, createGroup, save };
  }),
);

interface PersonDetailState {
  person: PersonResponse | null;
  groups: EconomicGroupResponse[];
  /** Possíveis duplicatas (mesmo documento ou nome parecido), candidatas a unificar neste cadastro. */
  duplicates: SimilarPerson[];
  /** Definições e tabelas auxiliares, para mostrar os campos customizados com rótulo e o nome da opção. */
  fields: CustomFieldResponse[];
  tables: LookupTableResponse[];
  busy: boolean;
  errorCode: string | null;
  done: 'merged' | 'anonymized' | null;
}

/** Detalhe da pessoa e as ações de ciclo de vida: unificar um duplicado, anonimizar (LGPD) e excluir. */
export const PersonDetailStore = signalStore(
  withState<PersonDetailState>({
    person: null,
    groups: [],
    duplicates: [],
    fields: [],
    tables: [],
    busy: false,
    errorCode: null,
    done: null,
  }),
  withMethods((store, api = inject(CrmApi)) => {
    async function load(id: string): Promise<void> {
      const [person, groups, fields, tables] = await Promise.all([
        api.person(id),
        api.economicGroups(),
        api.customFields(),
        api.lookupTables(),
      ]);
      patchState(store, {
        person: person.ok ? person.value : null,
        groups: groups.ok ? groups.value : [],
        fields: fields.ok ? fields.value : [],
        tables: tables.ok ? tables.value : [],
        errorCode: person.ok ? null : person.code,
      });
      await loadDuplicates();
    }

    async function loadDuplicates(): Promise<void> {
      const person = store.person();
      const duplicates = person ? await api.similar(person.name, person.document, person.id) : null;
      patchState(store, { duplicates: duplicates?.ok ? duplicates.value : [] });
    }

    async function run(
      call: () => ReturnType<CrmApi['merge']>,
      done: 'merged' | 'anonymized',
    ): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null, done: null });
      const result = await call();
      patchState(
        store,
        result.ok
          ? { busy: false, person: result.value, done }
          : { busy: false, errorCode: result.code },
      );
      return result.ok;
    }

    async function merge(duplicateId: string): Promise<boolean> {
      const merged = await run(() => api.merge(store.person()!.id, duplicateId), 'merged');
      if (merged) {
        await loadDuplicates();
      }
      return merged;
    }

    const anonymize = (reason: string) =>
      run(() => api.anonymize(store.person()!.id, reason), 'anonymized');

    async function remove(): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.deletePerson(store.person()!.id);
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      return result.ok;
    }

    return { load, merge, anonymize, remove };
  }),
);
