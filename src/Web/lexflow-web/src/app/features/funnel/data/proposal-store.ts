import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  OpportunityResponse,
  ProposalInput,
  ProposalResponse,
  ProposalTemplateResponse,
} from '../../../api/types.gen';
import type { ApiResult, FieldErrors } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import { FunnelApi, ProposalStep } from './funnel-api';
import { saveFile } from './save-file';

interface ProposalState {
  proposal: ProposalResponse | null;
  opportunity: OpportunityResponse | null;
  templates: ProposalTemplateResponse[];
  /** Cliente criado (ou reaproveitado) no aceite. */
  clientPersonId: string | null;
  busy: boolean;
  errorCode: string | null;
  fieldErrors: FieldErrors | undefined;
}

/**
 * Uma proposta de honorários (RF-08): rascunho com itens, aprovação interna, envio com o DOCX congelado, aceite que
 * cria o cliente, recusa, devolução para rascunho, exclusão e o download do documento.
 */
export const ProposalStore = signalStore(
  withState<ProposalState>({
    proposal: null,
    opportunity: null,
    templates: [],
    clientPersonId: null,
    busy: false,
    errorCode: null,
    fieldErrors: undefined,
  }),
  withMethods((store, api = inject(FunnelApi), access = inject(AccessStore)) => {
    /** Carrega a proposta (ou só a oportunidade, para uma proposta nova) e os modelos do escritório. */
    async function load(proposalId: string | null, opportunityId: string | null): Promise<void> {
      const [proposal, templates] = await Promise.all([
        proposalId ? api.proposal(proposalId) : Promise.resolve(null),
        api.templates(),
      ]);
      const ownerId = proposal?.ok ? proposal.value.opportunityId : opportunityId;
      const opportunity =
        ownerId && access.can('crm.opportunities.read') ? await api.opportunity(ownerId) : null;
      patchState(store, {
        proposal: proposal?.ok ? proposal.value : null,
        opportunity: opportunity?.ok ? opportunity.value : null,
        templates: templates.ok ? templates.value.templates : [],
        errorCode: proposal && !proposal.ok ? proposal.code : null,
      });
    }

    async function change(call: () => Promise<ApiResult<ProposalResponse>>): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null, fieldErrors: undefined });
      const result = await call();
      patchState(
        store,
        result.ok
          ? { busy: false, proposal: result.value }
          : { busy: false, errorCode: result.code, fieldErrors: result.errors },
      );
      return result.ok;
    }

    /** Cria o rascunho na oportunidade (a da rota) ou atualiza; devolve a proposta gravada. */
    async function save(
      input: ProposalInput,
      opportunityId: string | null,
    ): Promise<ProposalResponse | null> {
      const existing = store.proposal();
      const saved = await change(() =>
        existing
          ? api.updateProposal(existing.id, input)
          : api.createProposal(opportunityId ?? store.opportunity()!.id, input),
      );
      return saved ? store.proposal() : null;
    }

    const step = (name: ProposalStep, note: string | null = null) =>
      change(() => api.step(store.proposal()!.id, name, note?.trim() || null));

    const send = () => change(() => api.send(store.proposal()!.id));

    async function accept(): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.accept(store.proposal()!.id);
      patchState(
        store,
        result.ok
          ? {
              busy: false,
              proposal: result.value.proposal,
              clientPersonId: result.value.clientPersonId,
            }
          : { busy: false, errorCode: result.code },
      );
      return result.ok;
    }

    async function remove(): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.deleteProposal(store.proposal()!.id);
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      return result.ok;
    }

    async function download(): Promise<void> {
      const proposal = store.proposal()!;
      const result = await api.document(proposal.id);
      if (result.ok) {
        saveFile(result.value, `${proposal.title}.docx`);
      } else {
        patchState(store, { errorCode: result.code });
      }
    }

    return { load, save, step, send, accept, remove, download };
  }),
);
