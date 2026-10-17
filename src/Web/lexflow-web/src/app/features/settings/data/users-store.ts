import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type {
  DataScope,
  InvitationResponse,
  MemberResponse,
  RoleAssignmentInput,
  RoleResponse,
} from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import { SettingsApi } from './settings-api';

/** Equipe, área ou filial que pode ser referência de um escopo de dados. */
export interface ScopeReference {
  id: string;
  name: string;
}

interface UsersState {
  members: MemberResponse[];
  invitations: InvitationResponse[];
  roles: RoleResponse[];
  references: Record<'team' | 'practiceArea' | 'branch', ScopeReference[]>;
  busy: boolean;
  errorCode: string | null;
}

const initialState: UsersState = {
  members: [],
  invitations: [],
  roles: [],
  references: { team: [], practiceArea: [], branch: [] },
  busy: false,
  errorCode: null,
};

/** Membros do escritório (perfis com escopo de dados, remoção) e convites (reenviar, cancelar). */
export const UsersStore = signalStore(
  withState(initialState),
  withComputed(({ roles }) => ({
    roleNames: computed(() => new Map(roles().map((r) => [r.id, r.name]))),
  })),
  withMethods((store, api = inject(SettingsApi), access = inject(AccessStore)) => {
    async function load(): Promise<void> {
      const [members, invitations, roles] = await Promise.all([
        api.members(),
        api.invitations(),
        api.roles(),
      ]);
      if (!members.ok || !invitations.ok || !roles.ok) {
        const failed = [members, invitations, roles].find((r) => !r.ok);
        patchState(store, { errorCode: failed && !failed.ok ? failed.code : null });
        return;
      }

      patchState(store, {
        members: members.value,
        invitations: invitations.value,
        roles: roles.value,
      });

      // Escopos por equipe, área ou filial só para quem também administra a estrutura.
      if (access.can('settings.structure.manage')) {
        const [teams, areas, branches] = await Promise.all([
          api.teams(),
          api.practiceAreas(),
          api.branches(),
        ]);
        patchState(store, {
          references: {
            team: teams.ok ? teams.value : [],
            practiceArea: areas.ok ? areas.value : [],
            branch: branches.ok ? branches.value : [],
          },
        });
      }
    }

    /** Executa a alteração e, se deu certo, recarrega a tela. */
    async function change(call: () => Promise<ApiResult<unknown>>): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await call();
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      if (result.ok) {
        await load();
      }

      return result.ok;
    }

    return {
      load,

      referencesFor(scope: DataScope): ScopeReference[] {
        return scope === 'team' || scope === 'practiceArea' || scope === 'branch'
          ? store.references()[scope]
          : [];
      },

      assignRoles(memberId: string, roles: RoleAssignmentInput[]): Promise<boolean> {
        return change(() => api.assignRoles(memberId, roles));
      },

      removeMember(memberId: string): Promise<boolean> {
        return change(() => api.removeMember(memberId));
      },

      resend(invitationId: string): Promise<boolean> {
        return change(() => api.resendInvitation(invitationId));
      },

      revoke(invitationId: string): Promise<boolean> {
        return change(() => api.revokeInvitation(invitationId));
      },
    };
  }),
);
