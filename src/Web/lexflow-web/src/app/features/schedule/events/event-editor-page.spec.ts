import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import type { CalendarEventResponse } from '../../../api/types.gen';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import { AccessStore } from '../../../core/organization/access-store';
import { ScheduleApi } from '../data/schedule-api';
import { EventEditorPage } from './event-editor-page';

const existing: CalendarEventResponse = {
  id: 'event-1',
  caseId: null,
  title: 'Existing title',
  description: 'Existing description',
  startsAt: '2026-10-22T13:00:00Z',
  endsAt: '2026-10-22T14:00:00Z',
  assigneeUserId: 'member-1',
  location: 'Office',
  status: 'scheduled',
};

describe('EventEditorPage', () => {
  it('keeps the edited form draft after the server rejects an edit', async () => {
    const edit = vi.fn().mockResolvedValue({ ok: false, code: 'scheduling.event_invalid' });
    await TestBed.configureTestingModule({
      imports: [EventEditorPage, translocoTesting()],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ id: existing.id })) },
        },
        {
          provide: AccessStore,
          useValue: {
            can: (permission: string) =>
              ['schedule.events.read', 'schedule.events.manage'].includes(permission),
          },
        },
        {
          provide: ScheduleApi,
          useValue: {
            event: vi.fn().mockResolvedValue({ ok: true, value: existing }),
            options: vi.fn().mockResolvedValue({
              ok: true,
              value: { members: [{ userId: 'member-1', name: 'Member' }], teams: [] },
            }),
            edit,
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(EventEditorPage);
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const page = fixture.componentInstance as unknown as {
      form: {
        setValue(value: {
          title: string;
          description: string;
          location: string;
          startsAt: string;
          endsAt: string;
          assigneeUserId: string;
          caseId: string | null;
        }): void;
        getRawValue(): {
          title: string;
          description: string;
          location: string;
          startsAt: string;
          endsAt: string;
          assigneeUserId: string;
          caseId: string | null;
        };
      };
      save(): Promise<void>;
    };
    page.form.setValue({
      title: 'Draft title',
      description: 'Draft description',
      location: 'New room',
      startsAt: '2026-10-23T10:00',
      endsAt: '2026-10-23T11:00',
      assigneeUserId: 'member-1',
      caseId: null,
    });

    await page.save();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(edit).toHaveBeenCalledOnce();
    expect(page.form.getRawValue()).toMatchObject({
      title: 'Draft title',
      description: 'Draft description',
      location: 'New room',
    });
  });

  it('does not replace a draft when the linked case lookup finishes after the event load', async () => {
    let resolveCase!: (value: {
      ok: true;
      value: { id: string; internalFolder: string; cnj: string | null };
    }) => void;
    const caseLookup = new Promise<{
      ok: true;
      value: { id: string; internalFolder: string; cnj: string | null };
    }>((resolve) => {
      resolveCase = resolve;
    });
    const item = { ...existing, caseId: 'case-1' };
    await TestBed.configureTestingModule({
      imports: [EventEditorPage, translocoTesting()],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ id: item.id })) },
        },
        {
          provide: AccessStore,
          useValue: {
            can: (permission: string) =>
              ['schedule.events.read', 'schedule.events.manage', 'cases.read'].includes(permission),
          },
        },
        {
          provide: ScheduleApi,
          useValue: {
            event: vi.fn().mockResolvedValue({ ok: true, value: item }),
            case: vi.fn(() => caseLookup),
            options: vi.fn().mockResolvedValue({
              ok: true,
              value: { members: [{ userId: 'member-1', name: 'Member' }], teams: [] },
            }),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(EventEditorPage);
    fixture.detectChanges();
    const page = fixture.componentInstance as unknown as {
      form: {
        controls: { title: { setValue(value: string): void; value: string } };
      };
      store: { item: () => CalendarEventResponse | null };
    };
    for (let attempt = 0; attempt < 10 && !page.store.item(); attempt++) await Promise.resolve();
    expect(page.store.item()?.id).toBe(item.id);
    fixture.detectChanges();

    page.form.controls.title.setValue('Draft while case loads');
    resolveCase({ ok: true, value: { id: 'case-1', internalFolder: 'Folder 1', cnj: null } });
    for (let attempt = 0; attempt < 10; attempt++) await Promise.resolve();
    fixture.detectChanges();

    expect(page.form.controls.title.value).toBe('Draft while case loads');
  });
});
