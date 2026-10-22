import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  NotificationRulesResponse,
  NotificationSettingsResponse,
} from '../../../api/types.gen';
import { NotificationSettingsApi } from './notification-settings-api';

interface State {
  settings: NotificationSettingsResponse | null;
  rules: NotificationRulesResponse | null;
  settingsLoading: boolean;
  rulesLoading: boolean;
  settingsBusy: boolean;
  rulesBusy: boolean;
  settingsError: string | null;
  rulesError: string | null;
  settingsRevision: number;
  rulesRevision: number;
}
export const NotificationSettingsStore = signalStore(
  withState<State>({
    settings: null,
    rules: null,
    settingsLoading: false,
    rulesLoading: false,
    settingsBusy: false,
    rulesBusy: false,
    settingsError: null,
    rulesError: null,
    settingsRevision: 0,
    rulesRevision: 0,
  }),
  withMethods((store, api = inject(NotificationSettingsApi)) => {
    let settingsVersion = 0;
    let rulesVersion = 0;
    async function loadSettings() {
      const version = ++settingsVersion;
      patchState(store, { settingsLoading: true, settingsError: null, settings: null });
      const result = await api.settings();
      if (version !== settingsVersion) return;
      patchState(
        store,
        result.ok
          ? {
              settings: result.value,
              settingsLoading: false,
              settingsRevision: store.settingsRevision() + 1,
            }
          : { settings: null, settingsLoading: false, settingsError: result.code },
      );
    }
    async function saveSettings(body: Parameters<typeof api.saveSettings>[0]) {
      if (store.settingsBusy()) return;
      const version = ++settingsVersion;
      patchState(store, { settingsBusy: true, settingsError: null });
      const result = await api.saveSettings(body);
      if (version !== settingsVersion) return;
      patchState(
        store,
        result.ok
          ? {
              settings: result.value,
              settingsBusy: false,
              settingsRevision: store.settingsRevision() + 1,
            }
          : { settings: null, settingsBusy: false, settingsError: result.code },
      );
    }
    async function loadRules() {
      const version = ++rulesVersion;
      patchState(store, { rulesLoading: true, rulesError: null, rules: null });
      const result = await api.rules();
      if (version !== rulesVersion) return;
      patchState(
        store,
        result.ok
          ? { rules: result.value, rulesLoading: false, rulesRevision: store.rulesRevision() + 1 }
          : { rules: null, rulesLoading: false, rulesError: result.code },
      );
    }
    async function saveRules(requiredKinds: string[]) {
      if (store.rulesBusy()) return;
      const version = ++rulesVersion;
      patchState(store, { rulesBusy: true, rulesError: null });
      const result = await api.saveRules({ requiredKinds });
      if (version !== rulesVersion) return;
      patchState(
        store,
        result.ok
          ? { rules: result.value, rulesBusy: false, rulesRevision: store.rulesRevision() + 1 }
          : { rules: null, rulesBusy: false, rulesError: result.code },
      );
    }
    return { loadSettings, saveSettings, loadRules, saveRules };
  }),
);
