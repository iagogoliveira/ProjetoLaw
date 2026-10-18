import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type {
  ConflictCheckResponse,
  CustomFieldResponse,
  LookupItemResponse,
  LookupTableResponse,
  MemberResponse,
  OpportunityInput,
  OpportunityResponse,
  PersonSummary,
  ProposalSummary,
} from '../../../api/types.gen';
import type { ApiResult, FieldErrors } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import { FunnelApi } from './funnel-api';

interface OpportunityState {
  opportunity: OpportunityResponse | null;
  conflict: ConflictCheckResponse | null;
  proposals: ProposalSummary[];
  fields: CustomFieldResponse[];
  tables: LookupTableResponse[];
  members: MemberResponse[];
  /** Pessoas encontradas na busca do interessado já cadastrado. */
  people: PersonSummary[];
  busy: boolean;
  errorCode: string | null;
  fieldErrors: FieldErrors | undefined;
}

const activeItems = (tables: LookupTableResponse[], systemKey: string): LookupItemResponse[] =>
  (tables.find((t) => t.systemKey === systemKey)?.items ?? [])
    .filter((i) => i.isActive)
    .sort((a, b) => Number(a.position) - Number(b.position));

/**
 * Uma oportunidade do funil: cadastro e edição (interessado solto ou pessoa cadastrada, origem, responsável e campos
 * customizados), perda com motivo, reabertura, a verificação de conflito do interessado e as propostas dela.
 */
export const OpportunityStore = signalStore(
  withState<OpportunityState>({
    opportunity: null,
    conflict: null,
    proposals: [],
    fields: [],
    tables: [],
    members: [],
    people: [],
    busy: false,
    errorCode: null,
    fieldErrors: undefined,
  }),
  withComputed((store) => ({
    origins: computed(() => activeItems(store.tables(), 'opportunity_origins')),
    lossReasons: computed(() => activeItems(store.tables(), 'loss_reasons')),
    itemNames: computed(
      () => new Map(store.tables().flatMap((t) => t.items.map((i) => [i.id, i.name]))),
    ),
    memberNames: computed(
      () => new Map(store.members().map((m) => [m.userId, m.fullName ?? m.email ?? m.userId])),
    ),
    /** Pode aceitar uma proposta: a verificação do lado cliente foi prosseguida. */
    conflictCleared: computed(() => !!store.conflict()?.proceeded),
  })),
  withMethods((store, api = inject(FunnelApi), access = inject(AccessStore)) => {
    async function refreshConflict(): Promise<void> {
      const id = store.opportunity()?.conflictCheckId;
      const conflict = id ? await api.conflictCheck(id) : null;
      patchState(store, { conflict: conflict?.ok ? conflict.value : null });
    }

    async function refreshProposals(): Promise<void> {
      const id = store.opportunity()?.id;
      const proposals =
        id && access.can('crm.proposals.read') ? await api.proposals(id, null) : null;
      patchState(store, { proposals: proposals?.ok ? proposals.value.items : [] });
    }

    async function load(id: string | null): Promise<void> {
      const [fields, tables, members, opportunity] = await Promise.all([
        api.customFields(),
        api.lookupTables(),
        access.can('settings.users.read') ? api.members() : Promise.resolve(null),
        id ? api.opportunity(id) : Promise.resolve(null),
      ]);
      patchState(store, {
        fields: fields.ok ? fields.value : [],
        tables: tables.ok ? tables.value : [],
        members: members?.ok ? members.value : [],
        opportunity: opportunity?.ok ? opportunity.value : null,
        errorCode: opportunity && !opportunity.ok ? opportunity.code : null,
      });
      if (store.opportunity()) {
        await Promise.all([refreshConflict(), refreshProposals()]);
      }
    }

    async function searchPeople(term: string): Promise<void> {
      if (term.trim().length < 2) {
        patchState(store, { people: [] });
        return;
      }

      const result = await api.people(term.trim());
      patchState(store, { people: result.ok ? result.value.items : [] });
    }

    async function change(call: () => Promise<ApiResult<OpportunityResponse>>): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null, fieldErrors: undefined });
      const result = await call();
      patchState(
        store,
        result.ok
          ? { busy: false, opportunity: result.value }
          : { busy: false, errorCode: result.code, fieldErrors: result.errors },
      );
      return result.ok;
    }

    /** Cria ou atualiza; devolve a oportunidade gravada, ou nulo com os erros no estado. */
    async function save(input: OpportunityInput): Promise<OpportunityResponse | null> {
      const existing = store.opportunity();
      const saved = await change(() =>
        existing ? api.updateOpportunity(existing.id, input) : api.createOpportunity(input),
      );
      if (saved) {
        await refreshConflict();
      }
      return saved ? store.opportunity() : null;
    }

    const lose = (reasonId: string, note: string | null) =>
      change(() => api.lose(store.opportunity()!.id, reasonId, note));

    const reopen = () => change(() => api.move(store.opportunity()!.id, 'lead'));

    async function checkConflict(): Promise<void> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.checkConflict(store.opportunity()!.id);
      if (result.ok) {
        patchState(store, {
          busy: false,
          conflict: result.value,
          opportunity: { ...store.opportunity()!, conflictCheckId: result.value.id },
        });
      } else {
        patchState(store, { busy: false, errorCode: result.code });
      }
    }

    async function proceedConflict(justification: string | null): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.proceedConflict(store.conflict()!.id, justification?.trim() || null);
      patchState(
        store,
        result.ok
          ? { busy: false, conflict: result.value }
          : { busy: false, errorCode: result.code },
      );
      return result.ok;
    }

    async function remove(): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.deleteOpportunity(store.opportunity()!.id);
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      return result.ok;
    }

    return { load, searchPeople, save, lose, reopen, checkConflict, proceedConflict, remove };
  }),
);
