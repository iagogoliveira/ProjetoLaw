import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type {
  CaseBatchCommand,
  CaseBatchResult,
  CaseInput,
  CaseKind,
  CaseOptionsResponse,
  CaseResponse,
  CaseRestrictionsResponse,
  CasesPageResponse,
  ClosingInput,
  CnjInfoResponse,
  CreatePersonRequest,
  JsonElement,
  NumberingResponse,
  PeoplePageResponse,
  PersonKind,
  PersonResponse,
  PersonRoleKind,
  PreferenceResponse,
  RelationKind,
  RestrictionEntry,
  TimelinePageResponse,
} from '../../../api/types.gen';
import { ApiResult, toResult } from '../../../core/data/api-result';

/** Filtros da lista de processos, como a tela guarda e envia. */
export interface CaseFilters {
  q: string | null;
  status: string | null;
  kind: string | null;
  practiceAreaId: string | null;
  branchId: string | null;
  phaseId: string | null;
  responsibleId: string | null;
  teamId: string | null;
  personId: string | null;
}

export const NO_FILTERS: CaseFilters = {
  q: null,
  status: null,
  kind: null,
  practiceAreaId: null,
  branchId: null,
  phaseId: null,
  responsibleId: null,
  teamId: null,
  personId: null,
};

/** Único ponto das telas de processos que fala HTTP (seção 5.2), sobre o cliente gerado do OpenAPI. */
@Injectable({ providedIn: 'root' })
export class CasesApi {
  list(filters: CaseFilters, cursor: string | null): Promise<ApiResult<CasesPageResponse>> {
    const v = (value: string | null) => value ?? undefined;
    return toResult(
      sdk.listCases({
        query: {
          Q: v(filters.q),
          Status: v(filters.status),
          Kind: v(filters.kind),
          PracticeAreaId: v(filters.practiceAreaId),
          BranchId: v(filters.branchId),
          PhaseId: v(filters.phaseId),
          ResponsibleId: v(filters.responsibleId),
          TeamId: v(filters.teamId),
          PersonId: v(filters.personId),
          Cursor: v(cursor),
        },
      }),
    );
  }

  options(): Promise<ApiResult<CaseOptionsResponse>> {
    return toResult(sdk.getCaseOptions());
  }

  get(id: string): Promise<ApiResult<CaseResponse>> {
    return toResult(sdk.getCase({ path: { id } }));
  }

  create(kind: CaseKind, input: CaseInput): Promise<ApiResult<CaseResponse>> {
    return toResult(sdk.createCase({ body: { kind, ...input } }));
  }

  update(id: string, input: CaseInput): Promise<ApiResult<CaseResponse>> {
    return toResult(sdk.updateCase({ path: { id }, body: input }));
  }

  remove(id: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.deleteCase({ path: { id } }));
  }

  close(id: string, closing: ClosingInput): Promise<ApiResult<CaseResponse>> {
    return toResult(sdk.closeCase({ path: { id }, body: closing }));
  }

  reopen(id: string): Promise<ApiResult<CaseResponse>> {
    return toResult(sdk.reopenCase({ path: { id } }));
  }

  setStatus(id: string, status: 'active' | 'suspended'): Promise<ApiResult<CaseResponse>> {
    return toResult(sdk.changeCaseStatus({ path: { id }, body: { status } }));
  }

  link(id: string, childCaseId: string, kind: RelationKind): Promise<ApiResult<CaseResponse>> {
    return toResult(sdk.linkCase({ path: { id }, body: { childCaseId, kind } }));
  }

  unlink(id: string, relationId: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.unlinkCase({ path: { id, relationId } }));
  }

  lookupCnj(number: string): Promise<ApiResult<CnjInfoResponse>> {
    return toResult(sdk.lookupCnj({ query: { number } }));
  }

  restrictions(id: string): Promise<ApiResult<CaseRestrictionsResponse>> {
    return toResult(sdk.getCaseRestrictions({ path: { id } }));
  }

  setRestrictions(
    id: string,
    allowed: RestrictionEntry[],
    blocked: RestrictionEntry[],
  ): Promise<ApiResult<CaseRestrictionsResponse>> {
    return toResult(sdk.setCaseRestrictions({ path: { id }, body: { allowed, blocked } }));
  }

  timeline(
    id: string,
    kinds: string[],
    cursor: string | null,
  ): Promise<ApiResult<TimelinePageResponse>> {
    return toResult(
      sdk.getCaseTimeline({
        path: { id },
        query: { kind: kinds.length ? kinds : undefined, cursor: cursor ?? undefined },
      }),
    );
  }

  batch(command: CaseBatchCommand): Promise<ApiResult<CaseBatchResult>> {
    return toResult(sdk.runCaseBatch({ body: command }));
  }

  numbering(): Promise<ApiResult<NumberingResponse>> {
    return toResult(sdk.getCaseNumbering());
  }

  configureNumbering(
    format: string,
    areaCodes: Record<string, string>,
  ): Promise<ApiResult<NumberingResponse>> {
    return toResult(sdk.configureCaseNumbering({ body: { format, areaCodes } }));
  }

  // Pessoas do Crm, para as partes do processo.
  searchPeople(q: string): Promise<ApiResult<PeoplePageResponse>> {
    return toResult(sdk.listPeople({ query: { q } }));
  }

  createPerson(
    kind: PersonKind,
    name: string,
    document: string | null,
    role: PersonRoleKind,
  ): Promise<ApiResult<PersonResponse>> {
    const body: CreatePersonRequest = {
      kind,
      name,
      document,
      roles: [role],
      alternateName: null,
      email: null,
      phone: null,
      notes: null,
      fiscal: null,
      economicGroupId: null,
      addresses: null,
      contacts: null,
      customFields: null,
    };
    return toResult(sdk.createPerson({ body }));
  }

  // Preferências da tela (filtros salvos e colunas), por usuário no escritório.
  preference(key: string): Promise<ApiResult<PreferenceResponse>> {
    return toResult(sdk.getMyPreference({ query: { key } }));
  }

  savePreference(key: string, value: JsonElement): Promise<ApiResult<PreferenceResponse>> {
    return toResult(sdk.setMyPreference({ body: { key, value } }));
  }
}
