import { TestBed } from '@angular/core/testing';
import type { FormGroup } from '@angular/forms';
import { describe, expect, it, vi } from 'vitest';
import { translocoTesting } from '../../core/i18n/transloco-testing';
import { AccessStore } from '../../core/organization/access-store';
import { CalendarSyncApi } from './data/calendar-sync-api';
import { CalendarSyncPage } from './calendar-sync-page';

const connection = {
  id: 'connection-1',
  provider: 'google' as const,
  from: '2026-01-01',
  until: '2027-01-01',
  isActive: true,
  lastSyncedAt: null,
  lastErrorCode: null,
};

async function render(canManage: boolean) {
  const api = {
    connections: vi.fn().mockResolvedValue({
      ok: true,
      value: { isSimulation: true, isAvailable: true, items: [connection] },
    }),
    pending: vi.fn().mockResolvedValue({ ok: true, value: { items: [], nextCursor: null } }),
    connect: vi
      .fn()
      .mockImplementation(async (body: { provider: string; from: string; until: string }) => ({
        ok: true,
        value: { ...connection, ...body },
      })),
    disconnect: vi.fn().mockResolvedValue({ ok: true, value: connection }),
    queueSync: vi.fn().mockResolvedValue({ ok: true, value: connection }),
    resolve: vi.fn(),
  };
  await TestBed.configureTestingModule({
    imports: [CalendarSyncPage, translocoTesting()],
    providers: [
      { provide: CalendarSyncApi, useValue: api },
      {
        provide: AccessStore,
        useValue: {
          can: (permission: string) =>
            permission === 'identity.profile.read' ||
            (canManage && permission === 'identity.profile.manage'),
        },
      },
    ],
  }).compileComponents();
  const fixture = TestBed.createComponent(CalendarSyncPage);
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, api };
}

describe('CalendarSyncPage', () => {
  it('does not replace a period being edited when focus moves around the page', async () => {
    const { fixture, api } = await render(true);
    const page = fixture.componentInstance as unknown as {
      form: FormGroup;
      savePeriod: () => Promise<void>;
    };
    page.form.setValue({ provider: 'microsoft365', from: '2026-03-01', until: '2026-08-01' });
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    button.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));
    fixture.detectChanges();
    expect(page.form.getRawValue()).toEqual({
      provider: 'microsoft365',
      from: '2026-03-01',
      until: '2026-08-01',
    });
    await page.savePeriod();
    expect(api.connect).toHaveBeenCalledWith({
      provider: 'microsoft365',
      from: '2026-03-01',
      until: '2026-08-01',
    });
  });

  it('keeps profile-read users in consultation mode', async () => {
    const { fixture, api } = await render(false);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.nativeElement.textContent).toMatch(
      /Minhas conexões|calendarSync\.connections\.title/,
    );
    expect(api.connect).not.toHaveBeenCalled();
    expect(api.queueSync).not.toHaveBeenCalled();
  });
});
