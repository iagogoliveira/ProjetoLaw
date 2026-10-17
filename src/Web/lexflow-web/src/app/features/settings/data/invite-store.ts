import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { DataScope, InvitationResponse, RoleResponse } from '../../../api/types.gen';
import { OrganizationApi } from '../../../core/organization/data/organization-api';

interface InviteState {
  roles: RoleResponse[];
  sent: InvitationResponse | null;
  busy: boolean;
  errorCode: string | null;
}

const initialState: InviteState = { roles: [], sent: null, busy: false, errorCode: null };

/** Convite de uma pessoa ao escritório ativo, com o perfil e o escopo que ela recebe ao aceitar. */
export const InviteStore = signalStore(
  withState(initialState),
  withMethods((store, api = inject(OrganizationApi)) => ({
    async loadRoles(): Promise<void> {
      const roles = await api.roles();
      patchState(store, roles.ok ? { roles: roles.value } : { errorCode: roles.code });
    },

    async invite(email: string, roleId: string, scope: DataScope): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null, sent: null });
      const result = await api.invite(email, roleId, scope);
      patchState(
        store,
        result.ok ? { sent: result.value, busy: false } : { errorCode: result.code, busy: false },
      );
      return result.ok;
    },
  })),
);
