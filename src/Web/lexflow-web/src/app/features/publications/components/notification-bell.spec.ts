import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccessStore } from '../../../core/organization/access-store';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import { PublicationsLiveStore } from '../data/publications-live-store';
import { NotificationBell } from './notification-bell';

describe('NotificationBell', () => {
  it('labels scheduling notifications and links to their case when available', async () => {
    await TestBed.configureTestingModule({
      imports: [NotificationBell, translocoTesting()],
      providers: [
        provideRouter([]),
        {
          provide: AccessStore,
          useValue: { permissions: signal(new Set(['cases.read', 'publications.read'])) },
        },
        {
          provide: PublicationsLiveStore,
          useValue: {
            items: signal([
              {
                id: 'missed',
                kind: 'scheduling.deadline_missed',
                resourceId: 'deadline-1',
                caseId: 'case-1',
                createdAt: '2026-10-20T12:00:00Z',
                readAt: null,
              },
              {
                id: 'calendar',
                kind: 'scheduling.calendar_changed',
                resourceId: 'deadline-2',
                caseId: null,
                createdAt: '2026-10-20T12:00:00Z',
                readAt: null,
              },
              {
                id: 'publication',
                kind: 'publications.received',
                resourceId: 'publication-1',
                createdAt: '2026-10-20T12:00:00Z',
                readAt: null,
              },
            ]),
            nextCursor: signal(null),
            online: signal(true),
            errorCode: signal(null),
            loading: signal(false),
            refresh: vi.fn(),
            more: vi.fn(),
            read: vi.fn(),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(NotificationBell);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Prazo perdido');
    expect(element.textContent).toContain('Calendário alterado');
    expect(element.querySelector('a[href="/processos/case-1"]')).not.toBeNull();
    expect(element.querySelector('a[href="/processos"]')).not.toBeNull();
    expect(
      element.querySelector('a[href^="/publicacoes?publication=publication-1"]'),
    ).not.toBeNull();
  });
});
