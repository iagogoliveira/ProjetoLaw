import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type {
  NotificationRulesResponse,
  NotificationSettingsResponse,
} from '../../../api/types.gen';
import { NotificationSettingsApi } from './notification-settings-api';
import { NotificationSettingsStore } from './notification-settings-store';

const settings: NotificationSettingsResponse = {
  dailyDigestEnabled: true,
  includeAgenda: true,
  includeDeadlines: true,
  includePublications: true,
  mutedKinds: ['publications.received'],
  requiredKinds: ['scheduling.deadline_missed'],
  availableKinds: ['publications.received', 'scheduling.deadline_missed'],
};
const rules: NotificationRulesResponse = {
  requiredKinds: ['scheduling.deadline_missed'],
  mandatoryKinds: ['scheduling.deadline_missed'],
};
describe('NotificationSettingsStore', () => {
  function setup(api: Record<string, ReturnType<typeof vi.fn>>) {
    TestBed.configureTestingModule({
      providers: [NotificationSettingsStore, { provide: NotificationSettingsApi, useValue: api }],
    });
    return TestBed.inject(NotificationSettingsStore);
  }
  it('clears stale data after failed reads and ignores an older settings response', async () => {
    let resolveOld!: (value: { ok: true; value: NotificationSettingsResponse }) => void;
    const api = {
      settings: vi
        .fn()
        .mockImplementationOnce(
          () =>
            new Promise((resolve) => {
              resolveOld = resolve;
            }),
        )
        .mockResolvedValueOnce({ ok: false, code: 'identity.forbidden' }),
    };
    const store = setup(api);
    const oldRead = store.loadSettings();
    await store.loadSettings();
    expect(store.settings()).toBeNull();
    expect(store.settingsError()).toBe('identity.forbidden');
    resolveOld({ ok: true, value: settings });
    await oldRead;
    expect(store.settings()).toBeNull();
  });
  it('does not retain saved rules after a failed mutation', async () => {
    const store = setup({
      rules: vi.fn().mockResolvedValue({ ok: true, value: rules }),
      saveRules: vi.fn().mockResolvedValue({ ok: false, code: 'notifications.settings_invalid' }),
    });
    await store.loadRules();
    expect(store.rules()).toEqual(rules);
    await store.saveRules(['not-a-kind']);
    expect(store.rules()).toBeNull();
    expect(store.rulesError()).toBe('notifications.settings_invalid');
  });
});
