import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { NotificationSettingsInput, NotificationSettingsResponse } from '../../api/types.gen';
import { translocoTesting } from '../../core/i18n/transloco-testing';
import { AccessStore } from '../../core/organization/access-store';
import { NotificationSettingsApi } from './data/notification-settings-api';
import { NotificationSettingsPage } from './notification-settings-page';

const settings: NotificationSettingsResponse = {
  dailyDigestEnabled: false,
  includeAgenda: true,
  includeDeadlines: true,
  includePublications: true,
  mutedKinds: [],
  requiredKinds: ['scheduling.deadline_missed'],
  availableKinds: ['publications.received', 'scheduling.deadline_missed'],
};

describe('NotificationSettingsPage', () => {
  it('keeps an edited checkbox through focus changes and saves the choice', async () => {
    const saveSettings = vi.fn(async (body: NotificationSettingsInput) => ({
      ok: true as const,
      value: {
        ...settings,
        dailyDigestEnabled: body.dailyDigestEnabled,
        includeAgenda: body.includeAgenda,
        includeDeadlines: body.includeDeadlines,
        includePublications: body.includePublications,
        mutedKinds: body.mutedKinds ?? [],
      },
    }));
    await TestBed.configureTestingModule({
      imports: [NotificationSettingsPage, translocoTesting()],
      providers: [
        {
          provide: AccessStore,
          useValue: {
            can: (permission: string) =>
              permission === 'identity.profile.read' || permission === 'identity.profile.manage',
          },
        },
        {
          provide: NotificationSettingsApi,
          useValue: {
            settings: vi.fn().mockResolvedValue({ ok: true, value: settings }),
            saveSettings,
            rules: vi.fn(),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(NotificationSettingsPage);
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const checkboxInputs = [
      ...fixture.nativeElement.querySelectorAll('input[type="checkbox"]'),
    ] as HTMLInputElement[];
    const muteInput = checkboxInputs[checkboxInputs.length - 1];
    expect(muteInput).toBeDefined();
    muteInput.click();
    fixture.detectChanges();
    expect(muteInput.checked).toBe(true);

    const saveButton = fixture.nativeElement.querySelector(
      'form button[type="submit"]',
    ) as HTMLButtonElement | null;
    expect(saveButton).toBeDefined();
    saveButton!.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));
    fixture.detectChanges();
    expect(muteInput.checked).toBe(true);

    fixture.nativeElement
      .querySelector('form')
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(saveSettings).toHaveBeenCalledWith(
      expect.objectContaining({ mutedKinds: ['publications.received'] }),
    );
    expect(muteInput.checked).toBe(true);
  });

  it('keeps personal settings read-only for a user with read permission only', async () => {
    const saveSettings = vi.fn();
    await TestBed.configureTestingModule({
      imports: [NotificationSettingsPage, translocoTesting()],
      providers: [
        {
          provide: AccessStore,
          useValue: { can: (permission: string) => permission === 'identity.profile.read' },
        },
        {
          provide: NotificationSettingsApi,
          useValue: {
            settings: vi.fn().mockResolvedValue({ ok: true, value: settings }),
            saveSettings,
            rules: vi.fn(),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(NotificationSettingsPage);
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    expect(form).toBeDefined();
    expect(form.querySelector('button[type="submit"]')).toBeNull();
    const inputs = [...form.querySelectorAll('input[type="checkbox"]')] as HTMLInputElement[];
    expect(inputs.length).toBeGreaterThan(0);
    expect(inputs.every((input) => input.disabled)).toBe(true);

    const page = fixture.componentInstance as unknown as {
      savePersonal: () => void;
      toggleMuted: (kind: string, checked: boolean) => void;
    };
    const originalMutedKinds = fixture.componentInstance['personalForm'].controls.mutedKinds.value;
    page.toggleMuted('publications.received', true);
    page.savePersonal();
    expect(fixture.componentInstance['personalForm'].controls.mutedKinds.value).toEqual(
      originalMutedKinds,
    );
    expect(saveSettings).not.toHaveBeenCalled();
  });
});
