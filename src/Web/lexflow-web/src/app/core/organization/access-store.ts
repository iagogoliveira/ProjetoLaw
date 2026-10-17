import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type { MyAccessResponse } from '../../api/types.gen';
import { ActiveOrganization } from './active-organization';
import { OrganizationApi } from './data/organization-api';

interface AccessState {
  access: MyAccessResponse | null;
  /** O escritório exige MFA e a conta ainda não tem segundo fator: o shell avisa. */
  mfaMissing: boolean;
  loading: boolean;
  errorCode: string | null;
}

const initialState: AccessState = {
  access: null,
  mfaMissing: false,
  loading: false,
  errorCode: null,
};

/**
 * Acesso do usuário (`GET /api/v1/me/access`): escritórios, o ativo e as permissões nele, usados pelo `*lfCan`, pelos
 * guards e pela troca de organização. A UI só esconde; quem bloqueia é o servidor.
 */
export const AccessStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ access }) => ({
    loaded: computed(() => access() !== null),
    organizations: computed(() => access()?.organizations ?? []),
    activeOrganization: computed(() => {
      const current = access();
      return current?.organizations.find((o) => o.id === current.activeOrganizationId) ?? null;
    }),
    permissions: computed(() => new Set(access()?.permissions ?? [])),
    mfaRequired: computed(() => access()?.mfaRequired ?? false),
  })),
  withMethods((store, api = inject(OrganizationApi), active = inject(ActiveOrganization)) => {
    let pending: Promise<void> | null = null;

    async function fetchAccess(): Promise<void> {
      let result = await api.access();
      // A organização guardada na aba deixou de valer (membro removido): volta à escolha do servidor.
      if (!result.ok && result.code === 'organization.not_a_member' && active.id() !== null) {
        active.set(null);
        result = await api.access();
      }

      if (!result.ok) {
        patchState(store, { errorCode: result.code });
        return;
      }

      if (active.id() === null && result.value.activeOrganizationId) {
        active.set(result.value.activeOrganizationId);
      }

      let mfaMissing = false;
      if (result.value.mfaRequired) {
        const me = await api.me();
        mfaMissing = me.ok && me.value.mfaMethod === 'none';
      }

      patchState(store, { access: result.value, mfaMissing, errorCode: null });
    }

    async function reload(): Promise<void> {
      patchState(store, { loading: true });
      pending = fetchAccess().finally(() => patchState(store, { loading: false }));
      await pending;
    }

    return {
      can: (permission: string): boolean => store.permissions().has(permission),

      /** Carrega uma vez (os guards e o shell chamam sem repetir a requisição). */
      ensureLoaded(): Promise<void> {
        if (store.loaded()) {
          return Promise.resolve();
        }

        return pending ?? reload();
      },

      reload,

      /** Troca de organização (RF-02): grava a escolha no servidor e recarrega as permissões. */
      async switchTo(organizationId: string): Promise<boolean> {
        const result = await api.select(organizationId);
        if (!result.ok) {
          patchState(store, { errorCode: result.code });
          return false;
        }

        active.set(organizationId);
        await reload();
        return store.errorCode() === null;
      },

      /** Onboarding: cria o escritório e já entra nele. */
      async createOrganization(name: string): Promise<boolean> {
        const result = await api.create(name);
        if (!result.ok) {
          patchState(store, { errorCode: result.code });
          return false;
        }

        active.set(result.value.id);
        await reload();
        return store.errorCode() === null;
      },

      /** Saída da conta: nada do acesso anterior fica na aba. */
      reset(): void {
        active.set(null);
        pending = null;
        patchState(store, initialState);
      },
    };
  }),
);
