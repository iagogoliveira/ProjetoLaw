import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type { PermissionGroupResponse, RoleResponse } from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import { SettingsApi } from './settings-api';

export const OFFICE_ADMIN = 'office_admin';

interface RolesState {
  roles: RoleResponse[];
  groups: PermissionGroupResponse[];
  selectedId: string | null;
  busy: boolean;
  saved: boolean;
  errorCode: string | null;
}

const initialState: RolesState = {
  roles: [],
  groups: [],
  selectedId: null,
  busy: false,
  saved: false,
  errorCode: null,
};

/** Perfis do escritório e a matriz de permissões (catálogo por grupo). O Administrador do escritório tem todas. */
export const RolesStore = signalStore(
  withState(initialState),
  withComputed(({ roles, selectedId }) => ({
    selected: computed(() => roles().find((r) => r.id === selectedId()) ?? null),
  })),
  withMethods((store, api = inject(SettingsApi), access = inject(AccessStore)) => {
    async function load(): Promise<void> {
      const [roles, groups] = await Promise.all([api.roles(), api.permissions()]);
      if (!roles.ok || !groups.ok) {
        patchState(store, { errorCode: !roles.ok ? roles.code : !groups.ok ? groups.code : null });
        return;
      }

      const selectedId = roles.value.some((r) => r.id === store.selectedId())
        ? store.selectedId()
        : (roles.value[0]?.id ?? null);
      patchState(store, { roles: roles.value, groups: groups.value, selectedId });
    }

    async function change(
      call: () => Promise<ApiResult<unknown>>,
      selectAfter?: (value: unknown) => string | null,
    ): Promise<boolean> {
      patchState(store, { busy: true, saved: false, errorCode: null });
      const result = await call();
      if (!result.ok) {
        patchState(store, { busy: false, errorCode: result.code });
        return false;
      }

      if (selectAfter) {
        patchState(store, { selectedId: selectAfter(result.value) });
      }

      await load();
      // As permissões de quem está logado podem ter mudado (vale em até 5 s no servidor).
      await access.reload();
      patchState(store, { busy: false, saved: true });
      return true;
    }

    return {
      load,

      select(id: string): void {
        patchState(store, { selectedId: id, saved: false, errorCode: null });
      },

      /** Pode renomear e excluir perfis (os padrão também, menos o Administrador do escritório). */
      canManage: (): boolean => access.can('settings.roles.manage'),

      /** A matriz do perfil não pode ser editada: Administrador do escritório, ou quem não gerencia perfis. */
      isLocked: (role: RoleResponse | null): boolean =>
        !role || role.systemKey === OFFICE_ADMIN || !access.can('settings.roles.manage'),

      create: (name: string, permissions: string[]) =>
        change(
          () => api.createRole({ name, description: null, permissions }),
          (role) => (role as RoleResponse).id,
        ),

      update: (id: string, name: string, description: string | null) =>
        change(() => api.updateRole(id, { name, description })),

      setPermissions: (id: string, permissions: string[]) =>
        change(() => api.setRolePermissions(id, permissions)),

      remove: (id: string) =>
        change(
          () => api.deleteRole(id),
          () => null,
        ),
    };
  }),
);
