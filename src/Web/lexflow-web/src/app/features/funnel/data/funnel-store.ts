import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type {
  OpportunityStage,
  OpportunityStageSummary,
  OpportunitySummary,
  ProposalSummary,
} from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { FunnelApi } from './funnel-api';

/** Colunas do kanban, na ordem do funil (RF-08). */
export const STAGES: readonly OpportunityStage[] = ['lead', 'meeting', 'proposal', 'won', 'lost'];

/** Etapas para onde um cartão pode ser arrastado (ganhar é pelo aceite da proposta; perder, pela ação própria). */
export const OPEN_STAGES: readonly OpportunityStage[] = ['lead', 'meeting', 'proposal'];

interface Column {
  items: OpportunitySummary[];
  nextCursor: string | null;
}

type Columns = Record<OpportunityStage, Column>;

const emptyColumns = (): Columns =>
  Object.fromEntries(STAGES.map((s) => [s, { items: [], nextCursor: null }])) as unknown as Columns;

interface FunnelState {
  columns: Columns;
  summary: OpportunityStageSummary[];
  /** Fila de quem aprova: propostas aguardando aprovação. */
  approvals: ProposalSummary[];
  loading: boolean;
  errorCode: string | null;
}

/**
 * Kanban do funil: uma coluna por etapa, com o resumo (quantidade e valor estimado) no cabeçalho, e a fila de
 * propostas aguardando aprovação para quem aprova. Mover um cartão aparece na hora e volta se o servidor recusar.
 */
export const FunnelStore = signalStore(
  withState<FunnelState>({
    columns: emptyColumns(),
    summary: [],
    approvals: [],
    loading: false,
    errorCode: null,
  }),
  withComputed((store) => ({
    totals: computed(
      () =>
        new Map(
          store
            .summary()
            .map((s) => [s.stage, { count: Number(s.count), value: Number(s.estimatedValue) }]),
        ),
    ),
  })),
  withMethods((store, api = inject(FunnelApi), access = inject(AccessStore)) => {
    async function refreshSummary(): Promise<void> {
      const summary = await api.summary();
      if (summary.ok) {
        patchState(store, { summary: summary.value });
      }
    }

    async function load(): Promise<void> {
      patchState(store, { loading: true, errorCode: null });
      const pages = await Promise.all(STAGES.map((stage) => api.opportunities(stage, null)));
      const failed = pages.find((p) => !p.ok);
      const columns = emptyColumns();
      STAGES.forEach((stage, index) => {
        const page = pages[index];
        if (page.ok) {
          columns[stage] = { items: page.value.items, nextCursor: page.value.nextCursor };
        }
      });
      patchState(store, {
        columns,
        loading: false,
        errorCode: failed && !failed.ok ? failed.code : null,
      });

      await refreshSummary();
      if (access.can('crm.proposals.approve')) {
        const approvals = await api.proposals(null, 'pendingApproval');
        patchState(store, { approvals: approvals.ok ? approvals.value.items : [] });
      }
    }

    async function loadMore(stage: OpportunityStage): Promise<void> {
      const column = store.columns()[stage];
      if (!column.nextCursor) {
        return;
      }

      const page = await api.opportunities(stage, column.nextCursor);
      if (page.ok) {
        patchState(store, {
          columns: {
            ...store.columns(),
            [stage]: {
              items: [...column.items, ...page.value.items],
              nextCursor: page.value.nextCursor,
            },
          },
        });
      }
    }

    /** Move o cartão para outra etapa aberta (reabre a perdida); devolve se o servidor aceitou. */
    async function move(
      id: string,
      from: OpportunityStage,
      to: OpportunityStage,
      index = 0,
    ): Promise<boolean> {
      if (from === to || !OPEN_STAGES.includes(to)) {
        return false;
      }

      const before = store.columns();
      const card = before[from].items.find((o) => o.id === id);
      if (!card) {
        return false;
      }

      const target = [...before[to].items];
      target.splice(Math.min(index, target.length), 0, { ...card, stage: to });
      patchState(store, {
        errorCode: null,
        columns: {
          ...before,
          [from]: { ...before[from], items: before[from].items.filter((o) => o.id !== id) },
          [to]: { ...before[to], items: target },
        },
      });

      const result = await api.move(id, to);
      if (!result.ok) {
        patchState(store, { columns: before, errorCode: result.code });
        return false;
      }

      await refreshSummary();
      return true;
    }

    return { load, loadMore, move };
  }),
);
