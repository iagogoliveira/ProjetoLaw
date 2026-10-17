import { Injectable } from '@angular/core';
import {
  acceptInvitation,
  createInvitation,
  createOrganization,
  getMe,
  getMyAccess,
  listRoles,
  previewInvitation,
  selectActiveOrganization,
} from '../../../api/sdk.gen';
import type {
  DataScope,
  InvitationAcceptedResponse,
  InvitationPreviewResponse,
  InvitationResponse,
  MeResponse,
  MyAccessResponse,
  OrganizationCreatedResponse,
  RoleResponse,
} from '../../../api/types.gen';
import { ApiResult, toResult } from '../../data/api-result';

/** Chamadas de organização, acesso e convites do SPA (único ponto que fala HTTP, seção 5.2). */
@Injectable({ providedIn: 'root' })
export class OrganizationApi {
  /** Sem `organization`, vale a do interceptor (ou a que o servidor escolher: preferida ou única). */
  access(): Promise<ApiResult<MyAccessResponse>> {
    return toResult(getMyAccess());
  }

  /** Perfil do usuário: usado para saber se ele já tem segundo fator quando o escritório exige. */
  me(): Promise<ApiResult<MeResponse>> {
    return toResult(getMe());
  }

  /** Grava a escolha no servidor, para os próximos logins sem `X-Organization-Id`. */
  select(organizationId: string): Promise<ApiResult<unknown>> {
    return toResult(selectActiveOrganization({ body: { organizationId } }));
  }

  create(name: string): Promise<ApiResult<OrganizationCreatedResponse>> {
    return toResult(createOrganization({ body: { name } }));
  }

  roles(): Promise<ApiResult<RoleResponse[]>> {
    return toResult(listRoles());
  }

  invite(email: string, roleId: string, scope: DataScope): Promise<ApiResult<InvitationResponse>> {
    return toResult(
      createInvitation({ body: { email, roles: [{ roleId, scope, scopeRefId: null }] } }),
    );
  }

  preview(token: string): Promise<ApiResult<InvitationPreviewResponse>> {
    return toResult(previewInvitation({ body: { token } }));
  }

  accept(token: string): Promise<ApiResult<InvitationAcceptedResponse>> {
    return toResult(acceptInvitation({ body: { token } }));
  }
}
