import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import type { MeResponse, SessionResponse, TotpSetupResponse } from '../../../api/types.gen';
import { AccountApi } from './account-api';

interface AccountState {
  profile: MeResponse | null;
  sessions: SessionResponse[];
  totpSetup: TotpSetupResponse | null;
  /** Mostrados uma única vez, logo depois de ligar o TOTP. */
  recoveryCodes: string[];
  loading: boolean;
  busy: boolean;
  errorCode: string | null;
}

const initialState: AccountState = {
  profile: null,
  sessions: [],
  totpSetup: null,
  recoveryCodes: [],
  loading: false,
  busy: false,
  errorCode: null,
};

/** Estado de "Minha conta": perfil, sessões abertas e ativação do segundo fator. */
export const AccountStore = signalStore(
  withState(initialState),
  withComputed(({ profile }) => ({
    mfaMethod: computed(() => profile()?.mfaMethod ?? 'none'),
    mfaEnabled: computed(() => (profile()?.mfaMethod ?? 'none') !== 'none'),
  })),
  withMethods((store, api = inject(AccountApi)) => {
    async function run(action: () => Promise<boolean>): Promise<void> {
      patchState(store, { busy: true, errorCode: null });
      try {
        await action();
      } finally {
        patchState(store, { busy: false });
      }
    }

    async function reload(): Promise<boolean> {
      const [profile, sessions] = await Promise.all([api.me(), api.sessions()]);
      if (!profile.ok || !sessions.ok) {
        patchState(store, {
          errorCode: !profile.ok ? profile.code : !sessions.ok ? sessions.code : null,
        });
        return false;
      }

      patchState(store, { profile: profile.value, sessions: sessions.value });
      return true;
    }

    return {
      async load(): Promise<void> {
        patchState(store, { loading: true, errorCode: null });
        await reload();
        patchState(store, { loading: false });
      },

      endSession: (id: string) =>
        run(async () => {
          const result = await api.endSession(id);
          if (!result.ok) {
            patchState(store, { errorCode: result.code });
            return false;
          }

          patchState(store, { sessions: store.sessions().filter((s) => s.id !== id) });
          return true;
        }),

      startTotp: () =>
        run(async () => {
          const result = await api.startTotp();
          patchState(store, result.ok ? { totpSetup: result.value } : { errorCode: result.code });
          return result.ok;
        }),

      confirmTotp: (code: string) =>
        run(async () => {
          const result = await api.confirmTotp(code);
          if (!result.ok) {
            patchState(store, { errorCode: result.code });
            return false;
          }

          patchState(store, { totpSetup: null, recoveryCodes: result.value.recoveryCodes });
          return reload();
        }),

      cancelTotp(): void {
        patchState(store, { totpSetup: null, errorCode: null });
      },

      enableEmailMfa: () =>
        run(async () => {
          const result = await api.enableEmailMfa();
          if (!result.ok) {
            patchState(store, { errorCode: result.code });
            return false;
          }

          return reload();
        }),
    };
  }),
);
