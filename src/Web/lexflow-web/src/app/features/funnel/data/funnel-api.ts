import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type {
  AcceptedProposalResponse,
  ConflictCheckResponse,
  CustomFieldResponse,
  LookupTableResponse,
  MemberResponse,
  OpportunitiesPageResponse,
  OpportunityInput,
  OpportunityResponse,
  OpportunityStage,
  OpportunityStageSummary,
  PeoplePageResponse,
  ProposalInput,
  ProposalResponse,
  ProposalsPageResponse,
  ProposalStatus,
  ProposalTemplateResponse,
  ProposalTemplatesResponse,
} from '../../../api/types.gen';
import { ApiResult, toResult } from '../../../core/data/api-result';

/**
 * O cliente gerado roda sobre o HttpClient do Angular, que lê a resposta como JSON por padrão; nos downloads (DOCX) a
 * opção vai direto para o `HttpRequest`, que o tipo das opções não declara.
 */
const AS_BLOB = { responseType: 'blob' } as object;

/** Passos do fluxo da proposta que só mudam a situação (com uma observação opcional). */
export type ProposalStep = 'submit' | 'approve' | 'return' | 'decline';

/** Único ponto das telas do funil e das propostas que fala HTTP (seção 5.2), sobre o cliente gerado do OpenAPI. */
@Injectable({ providedIn: 'root' })
export class FunnelApi {
  // Oportunidades
  opportunities(
    stage: OpportunityStage | null,
    cursor: string | null,
  ): Promise<ApiResult<OpportunitiesPageResponse>> {
    return toResult(
      sdk.listOpportunities({ query: { stage: stage ?? undefined, cursor: cursor ?? undefined } }),
    );
  }

  summary(): Promise<ApiResult<OpportunityStageSummary[]>> {
    return toResult(sdk.summarizeOpportunities());
  }

  opportunity(id: string): Promise<ApiResult<OpportunityResponse>> {
    return toResult(sdk.getOpportunity({ path: { id } }));
  }

  createOpportunity(body: OpportunityInput): Promise<ApiResult<OpportunityResponse>> {
    return toResult(sdk.createOpportunity({ body }));
  }

  updateOpportunity(id: string, body: OpportunityInput): Promise<ApiResult<OpportunityResponse>> {
    return toResult(sdk.updateOpportunity({ path: { id }, body }));
  }

  deleteOpportunity(id: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.deleteOpportunity({ path: { id } }));
  }

  move(id: string, stage: OpportunityStage): Promise<ApiResult<OpportunityResponse>> {
    return toResult(sdk.moveOpportunity({ path: { id }, body: { stage } }));
  }

  lose(id: string, reasonId: string, note: string | null): Promise<ApiResult<OpportunityResponse>> {
    return toResult(sdk.loseOpportunity({ path: { id }, body: { reasonId, note } }));
  }

  checkConflict(id: string): Promise<ApiResult<ConflictCheckResponse>> {
    return toResult(sdk.checkOpportunityConflict({ path: { id } }));
  }

  conflictCheck(id: string): Promise<ApiResult<ConflictCheckResponse>> {
    return toResult(sdk.getConflictCheck({ path: { id } }));
  }

  proceedConflict(
    id: string,
    justification: string | null,
  ): Promise<ApiResult<ConflictCheckResponse>> {
    return toResult(sdk.proceedConflict({ path: { id }, body: { justification } }));
  }

  // Propostas
  proposals(
    opportunityId: string | null,
    status: ProposalStatus | null,
  ): Promise<ApiResult<ProposalsPageResponse>> {
    return toResult(
      sdk.listProposals({
        query: { opportunityId: opportunityId ?? undefined, status: status ?? undefined },
      }),
    );
  }

  proposal(id: string): Promise<ApiResult<ProposalResponse>> {
    return toResult(sdk.getProposal({ path: { id } }));
  }

  createProposal(opportunityId: string, body: ProposalInput): Promise<ApiResult<ProposalResponse>> {
    return toResult(sdk.createProposal({ path: { id: opportunityId }, body }));
  }

  updateProposal(id: string, body: ProposalInput): Promise<ApiResult<ProposalResponse>> {
    return toResult(sdk.updateProposal({ path: { id }, body }));
  }

  deleteProposal(id: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.deleteProposal({ path: { id } }));
  }

  step(id: string, step: ProposalStep, note: string | null): Promise<ApiResult<ProposalResponse>> {
    const options = { path: { id }, body: { note } };
    switch (step) {
      case 'submit':
        return toResult(sdk.submitProposal(options));
      case 'approve':
        return toResult(sdk.approveProposal(options));
      case 'return':
        return toResult(sdk.returnProposal(options));
      default:
        return toResult(sdk.declineProposal(options));
    }
  }

  send(id: string): Promise<ApiResult<ProposalResponse>> {
    return toResult(sdk.sendProposal({ path: { id } }));
  }

  accept(id: string): Promise<ApiResult<AcceptedProposalResponse>> {
    return toResult(sdk.acceptProposal({ path: { id } }));
  }

  document(id: string): Promise<ApiResult<Blob>> {
    return toResult(sdk.getProposalDocument({ path: { id }, ...AS_BLOB }));
  }

  // Modelos
  templates(): Promise<ApiResult<ProposalTemplatesResponse>> {
    return toResult(sdk.listProposalTemplates());
  }

  defaultTemplate(): Promise<ApiResult<Blob>> {
    return toResult(sdk.getDefaultProposalTemplate({ ...AS_BLOB }));
  }

  uploadTemplate(name: string, file: File): Promise<ApiResult<ProposalTemplateResponse>> {
    return toResult(sdk.uploadProposalTemplate({ body: { name, file } }));
  }

  deleteTemplate(id: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.deleteProposalTemplate({ path: { id } }));
  }

  // Apoio dos formulários
  customFields(): Promise<ApiResult<CustomFieldResponse[]>> {
    return toResult(sdk.listCustomFields({ query: { entity: 'opportunities' } }));
  }

  lookupTables(): Promise<ApiResult<LookupTableResponse[]>> {
    return toResult(sdk.listLookupTables());
  }

  members(): Promise<ApiResult<MemberResponse[]>> {
    return toResult(sdk.listMembers());
  }

  people(term: string): Promise<ApiResult<PeoplePageResponse>> {
    return toResult(sdk.listPeople({ query: { q: term } }));
  }
}
