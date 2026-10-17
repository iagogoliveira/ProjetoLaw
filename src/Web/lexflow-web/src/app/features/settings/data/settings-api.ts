import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type {
  BranchRequest,
  BranchResponse,
  CostCenterRequest,
  CostCenterResponse,
  CreateCustomFieldRequest,
  CreateRoleRequest,
  CustomFieldResponse,
  CustomFieldValuesResponse,
  InvitationResponse,
  LookupItemResponse,
  LookupTableResponse,
  MemberResponse,
  OrganizationDetailsResponse,
  PartnerRequest,
  PartnerResponse,
  PermissionGroupResponse,
  PracticeAreaRequest,
  PracticeAreaResponse,
  RoleAssignmentInput,
  RoleResponse,
  TeamRequest,
  TeamResponse,
  UpdateCustomFieldRequest,
  UpdateLookupItemRequest,
  UpdateRoleRequest,
} from '../../../api/types.gen';
import { ApiResult, toResult } from '../../../core/data/api-result';

/** Tipos de cadastro da estrutura e os nomes usados nas rotas da API. */
export type StructureKind = 'branches' | 'practiceAreas' | 'teams' | 'costCenters' | 'partners';

/** Único ponto das telas de configurações que fala HTTP (seção 5.2), sobre o cliente gerado do OpenAPI. */
@Injectable({ providedIn: 'root' })
export class SettingsApi {
  // Escritório
  organization(): Promise<ApiResult<OrganizationDetailsResponse>> {
    return toResult(sdk.getOrganization());
  }

  rename(name: string): Promise<ApiResult<OrganizationDetailsResponse>> {
    return toResult(sdk.renameOrganization({ body: { name } }));
  }

  setMfaPolicy(mfaRequired: boolean): Promise<ApiResult<unknown>> {
    return toResult(sdk.setMfaPolicy({ body: { mfaRequired } }));
  }

  // Membros e convites
  members(): Promise<ApiResult<MemberResponse[]>> {
    return toResult(sdk.listMembers());
  }

  assignRoles(memberId: string, roles: RoleAssignmentInput[]): Promise<ApiResult<unknown>> {
    return toResult(sdk.assignMemberRoles({ path: { id: memberId }, body: { roles } }));
  }

  removeMember(memberId: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.removeMember({ path: { id: memberId } }));
  }

  invitations(): Promise<ApiResult<InvitationResponse[]>> {
    return toResult(sdk.listInvitations());
  }

  resendInvitation(id: string): Promise<ApiResult<InvitationResponse>> {
    return toResult(sdk.resendInvitation({ path: { id } }));
  }

  revokeInvitation(id: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.revokeInvitation({ path: { id } }));
  }

  // Perfis
  roles(): Promise<ApiResult<RoleResponse[]>> {
    return toResult(sdk.listRoles());
  }

  permissions(): Promise<ApiResult<PermissionGroupResponse[]>> {
    return toResult(sdk.listPermissions());
  }

  createRole(body: CreateRoleRequest): Promise<ApiResult<RoleResponse>> {
    return toResult(sdk.createRole({ body }));
  }

  updateRole(id: string, body: UpdateRoleRequest): Promise<ApiResult<RoleResponse>> {
    return toResult(sdk.updateRole({ path: { id }, body }));
  }

  setRolePermissions(id: string, permissions: string[]): Promise<ApiResult<unknown>> {
    return toResult(sdk.updateRolePermissions({ path: { id }, body: { permissions } }));
  }

  deleteRole(id: string): Promise<ApiResult<unknown>> {
    return toResult(sdk.deleteRole({ path: { id } }));
  }

  // Estrutura
  branches(): Promise<ApiResult<BranchResponse[]>> {
    return toResult(sdk.listBranches());
  }

  practiceAreas(): Promise<ApiResult<PracticeAreaResponse[]>> {
    return toResult(sdk.listPracticeAreas());
  }

  teams(): Promise<ApiResult<TeamResponse[]>> {
    return toResult(sdk.listTeams());
  }

  costCenters(): Promise<ApiResult<CostCenterResponse[]>> {
    return toResult(sdk.listCostCenters());
  }

  partners(): Promise<ApiResult<PartnerResponse[]>> {
    return toResult(sdk.listPartners());
  }

  saveBranch(id: string | null, body: BranchRequest): Promise<ApiResult<BranchResponse>> {
    return toResult(id ? sdk.updateBranch({ path: { id }, body }) : sdk.createBranch({ body }));
  }

  savePracticeArea(
    id: string | null,
    body: PracticeAreaRequest,
  ): Promise<ApiResult<PracticeAreaResponse>> {
    return toResult(
      id ? sdk.updatePracticeArea({ path: { id }, body }) : sdk.createPracticeArea({ body }),
    );
  }

  saveTeam(id: string | null, body: TeamRequest): Promise<ApiResult<TeamResponse>> {
    return toResult(id ? sdk.updateTeam({ path: { id }, body }) : sdk.createTeam({ body }));
  }

  setTeamMembers(id: string, memberIds: string[]): Promise<ApiResult<TeamResponse>> {
    return toResult(sdk.setTeamMembers({ path: { id }, body: { memberIds } }));
  }

  saveCostCenter(
    id: string | null,
    body: CostCenterRequest,
  ): Promise<ApiResult<CostCenterResponse>> {
    return toResult(
      id ? sdk.updateCostCenter({ path: { id }, body }) : sdk.createCostCenter({ body }),
    );
  }

  savePartner(id: string | null, body: PartnerRequest): Promise<ApiResult<PartnerResponse>> {
    return toResult(id ? sdk.updatePartner({ path: { id }, body }) : sdk.createPartner({ body }));
  }

  deleteStructure(kind: StructureKind, id: string): Promise<ApiResult<unknown>> {
    const options = { path: { id } };
    switch (kind) {
      case 'branches':
        return toResult(sdk.deleteBranch(options));
      case 'practiceAreas':
        return toResult(sdk.deletePracticeArea(options));
      case 'teams':
        return toResult(sdk.deleteTeam(options));
      case 'costCenters':
        return toResult(sdk.deleteCostCenter(options));
      default:
        return toResult(sdk.deletePartner(options));
    }
  }

  // Campos customizados e tabelas auxiliares
  customFields(entity: string): Promise<ApiResult<CustomFieldResponse[]>> {
    return toResult(sdk.listCustomFields({ query: { entity } }));
  }

  createCustomField(body: CreateCustomFieldRequest): Promise<ApiResult<CustomFieldResponse>> {
    return toResult(sdk.createCustomField({ body }));
  }

  updateCustomField(
    id: string,
    body: UpdateCustomFieldRequest,
  ): Promise<ApiResult<CustomFieldResponse>> {
    return toResult(sdk.updateCustomField({ path: { id }, body }));
  }

  validateCustomFields(
    entity: string,
    values: Record<string, unknown>,
  ): Promise<ApiResult<CustomFieldValuesResponse>> {
    return toResult(sdk.validateCustomFieldValues({ body: { entity, values } }));
  }

  lookupTables(): Promise<ApiResult<LookupTableResponse[]>> {
    return toResult(sdk.listLookupTables());
  }

  createLookupTable(name: string): Promise<ApiResult<LookupTableResponse>> {
    return toResult(sdk.createLookupTable({ body: { name } }));
  }

  updateLookupTable(
    id: string,
    name: string,
    isActive: boolean,
  ): Promise<ApiResult<LookupTableResponse>> {
    return toResult(sdk.updateLookupTable({ path: { id }, body: { name, isActive } }));
  }

  createLookupItem(tableId: string, name: string): Promise<ApiResult<LookupItemResponse>> {
    return toResult(
      sdk.createLookupItem({ path: { id: tableId }, body: { name, position: null } }),
    );
  }

  updateLookupItem(
    id: string,
    body: UpdateLookupItemRequest,
  ): Promise<ApiResult<LookupItemResponse>> {
    return toResult(sdk.updateLookupItem({ path: { id }, body }));
  }
}
