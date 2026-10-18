import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type {
  CompanyRegistration,
  ConflictCheckResponse,
  ConflictChecksPageResponse,
  ConflictRisk,
  ConflictSide,
  CreatePersonRequest,
  CustomFieldResponse,
  EconomicGroupResponse,
  LookupTableResponse,
  PeoplePageResponse,
  PersonInput,
  PersonResponse,
  PersonRoleKind,
  SimilarPerson,
} from '../../../api/types.gen';
import { ApiResult, toResult } from '../../../core/data/api-result';

/** Consulta de conflito como a tela envia. */
export interface ConflictQuery {
  name: string;
  document: string | null;
  side: ConflictSide;
  contactEmails: string[];
}

/** Único ponto das telas de pessoas e conflito que fala HTTP (seção 5.2), sobre o cliente gerado do OpenAPI. */
@Injectable({ providedIn: 'root' })
export class CrmApi {
  // Pessoas
  people(
    q: string | null,
    role: PersonRoleKind | null,
    cursor: string | null,
  ): Promise<ApiResult<PeoplePageResponse>> {
    return toResult(
      sdk.listPeople({
        query: { q: q ?? undefined, role: role ?? undefined, cursor: cursor ?? undefined },
      }),
    );
  }

  person(id: string): Promise<ApiResult<PersonResponse>> {
    return toResult(sdk.getPerson({ path: { id } }));
  }

  createPerson(body: CreatePersonRequest): Promise<ApiResult<PersonResponse>> {
    return toResult(sdk.createPerson({ body }));
  }

  updatePerson(id: string, body: PersonInput): Promise<ApiResult<PersonResponse>> {
    return toResult(sdk.updatePerson({ path: { id }, body }));
  }

  deletePerson(id: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.deletePerson({ path: { id } }));
  }

  similar(
    name: string | null,
    document: string | null,
    excludeId: string | null,
  ): Promise<ApiResult<SimilarPerson[]>> {
    return toResult(
      sdk.findSimilarPeople({
        query: {
          name: name ?? undefined,
          document: document ?? undefined,
          excludeId: excludeId ?? undefined,
        },
      }),
    );
  }

  lookupCnpj(cnpj: string): Promise<ApiResult<CompanyRegistration>> {
    return toResult(sdk.lookupCnpj({ query: { cnpj } }));
  }

  merge(keepId: string, duplicateId: string): Promise<ApiResult<PersonResponse>> {
    return toResult(sdk.mergePeople({ path: { id: keepId }, body: { duplicateId } }));
  }

  anonymize(id: string, reason: string): Promise<ApiResult<PersonResponse>> {
    return toResult(sdk.anonymizePerson({ path: { id }, body: { reason } }));
  }

  economicGroups(): Promise<ApiResult<EconomicGroupResponse[]>> {
    return toResult(sdk.listEconomicGroups());
  }

  createEconomicGroup(name: string): Promise<ApiResult<EconomicGroupResponse>> {
    return toResult(sdk.createEconomicGroup({ body: { name } }));
  }

  // Campos customizados de pessoas (definições e tabelas auxiliares)
  customFields(): Promise<ApiResult<CustomFieldResponse[]>> {
    return toResult(sdk.listCustomFields({ query: { entity: 'people' } }));
  }

  lookupTables(): Promise<ApiResult<LookupTableResponse[]>> {
    return toResult(sdk.listLookupTables());
  }

  // Conflito de interesses
  checkConflict(query: ConflictQuery): Promise<ApiResult<ConflictCheckResponse>> {
    return toResult(sdk.checkConflict({ body: query }));
  }

  proceed(id: string, justification: string | null): Promise<ApiResult<ConflictCheckResponse>> {
    return toResult(sdk.proceedConflict({ path: { id }, body: { justification } }));
  }

  conflictChecks(
    risk: ConflictRisk | null,
    cursor: string | null,
  ): Promise<ApiResult<ConflictChecksPageResponse>> {
    return toResult(
      sdk.listConflictChecks({ query: { risk: risk ?? undefined, cursor: cursor ?? undefined } }),
    );
  }
}
