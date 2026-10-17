import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { OrganizationDetailsResponse } from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { SettingsApi } from './settings-api';

interface OrganizationSettingsState {
  organization: OrganizationDetailsResponse | null;
  busy: boolean;
  saved: boolean;
  errorCode: string | null;
}

const initialState: OrganizationSettingsState = {
  organization: null,
  busy: false,
  saved: false,
  errorCode: null,
};

/** Nome do escritório e MFA obrigatório para todos os membros (RF-01). */
export const OrganizationSettingsStore = signalStore(
  withState(initialState),
  withMethods((store, api = inject(SettingsApi), access = inject(AccessStore)) => ({
    async load(): Promise<void> {
      const result = await api.organization();
      patchState(store, result.ok ? { organization: result.value } : { errorCode: result.code });
    },

    async save(name: string, mfaRequired: boolean): Promise<boolean> {
      patchState(store, { busy: true, saved: false, errorCode: null });
      const renamed = await api.rename(name);
      const policy =
        renamed.ok && renamed.value.mfaRequired !== mfaRequired
          ? await api.setMfaPolicy(mfaRequired)
          : null;
      const failure = !renamed.ok ? renamed : policy && !policy.ok ? policy : null;
      if (failure && !failure.ok) {
        patchState(store, { busy: false, errorCode: failure.code });
        return false;
      }

      patchState(store, {
        busy: false,
        saved: true,
        organization: renamed.ok ? { ...renamed.value, mfaRequired } : null,
      });
      // O nome no topo e a exigência de MFA vêm do acesso.
      await access.reload();
      return true;
    },
  })),
);
