import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { CalculateDeadlineQuery, DeadlineCalculationResponse } from '../../../api/types.gen';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import { DeadlinesApi } from '../data/deadlines-api';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { DeadlineCreatePage } from './deadline-create-page';

const suggestion = {
  query: {
    caseId: 'case-1',
    courtId: null,
    stateCode: 'SP',
    municipality: null,
    startKind: 'eventDate',
    triggerDate: '2026-10-21',
    consultedOn: null,
    days: 5,
    mode: 'businessDays',
    doubled: false,
    internalLeadDays: 1,
  } as CalculateDeadlineQuery,
  result: {
    intimationOn: '2026-10-21',
    startsOn: '2026-10-22',
    internalDueOn: '2026-10-27',
    fatalDueOn: '2026-10-28',
    countedDays: 5,
    courtId: null,
    stateCode: 'SP',
    municipality: null,
    federalJustice: false,
    lines: [],
  } as DeadlineCalculationResponse,
};

describe('DeadlineCreatePage', () => {
  it('invalidates the selected case and confirmation when query parameters change', async () => {
    const params = new BehaviorSubject(convertToParamMap({ caseId: 'case-1' }));
    await TestBed.configureTestingModule({
      imports: [DeadlineCreatePage, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: params } },
        {
          provide: DeadlinesApi,
          useValue: {
            options: vi.fn().mockResolvedValue({ ok: true, value: { members: [], teams: [] } }),
            case: vi.fn((id: string) =>
              Promise.resolve({ ok: true, value: { id, internalFolder: id, cnjNumber: null } }),
            ),
          },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DeadlineCreatePage);
    await fixture.whenStable();
    const page = fixture.componentInstance as unknown as {
      onCalculated(value: typeof suggestion): void;
      confirmed: { set(value: boolean): void; (): boolean };
      caseId(): string | null;
      suggestion(): unknown;
    };
    page.onCalculated(suggestion);
    page.confirmed.set(true);
    params.next(convertToParamMap({ caseId: 'case-2' }));
    await fixture.whenStable();
    expect(page.caseId()).toBe('case-2');
    expect(page.suggestion()).toBeNull();
    expect(page.confirmed()).toBe(false);
  });

  it('does not navigate after a pending save finishes on a destroyed page', async () => {
    let finish!: (value: { ok: true; value: { id: string } }) => void;
    await TestBed.configureTestingModule({
      imports: [DeadlineCreatePage, translocoTesting()],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { queryParamMap: of(convertToParamMap({ caseId: 'case-1' })) },
        },
        {
          provide: DeadlinesApi,
          useValue: {
            options: vi.fn().mockResolvedValue({ ok: true, value: { members: [], teams: [] } }),
            case: vi.fn().mockResolvedValue({
              ok: true,
              value: { id: 'case-1', internalFolder: '001', cnjNumber: null },
            }),
            create: vi.fn(
              () =>
                new Promise((resolve) => {
                  finish = resolve;
                }),
            ),
          },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DeadlineCreatePage);
    await fixture.whenStable();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');
    const page = fixture.componentInstance as unknown as {
      form: { patchValue(value: object): void };
      onCalculated(value: typeof suggestion): void;
      confirmed: { set(value: boolean): void };
      submit(): Promise<void>;
    };
    page.form.patchValue({ type: 'Appeal', title: 'Appeal', assigneeUserId: 'user-1' });
    page.onCalculated(suggestion);
    page.confirmed.set(true);
    const saving = page.submit();
    fixture.destroy();
    finish({ ok: true, value: { id: 'deadline-1' } });
    await saving;
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not navigate or restore busy state when the case changes during save', async () => {
    let finish!: (value: { ok: true; value: { id: string } }) => void;
    const params = new BehaviorSubject(convertToParamMap({ caseId: 'case-1' }));
    await TestBed.configureTestingModule({
      imports: [DeadlineCreatePage, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: params } },
        {
          provide: DeadlinesApi,
          useValue: {
            options: vi.fn().mockResolvedValue({ ok: true, value: { members: [], teams: [] } }),
            case: vi.fn((id: string) =>
              Promise.resolve({ ok: true, value: { id, internalFolder: id, cnjNumber: null } }),
            ),
            create: vi.fn(
              () =>
                new Promise((resolve) => {
                  finish = resolve;
                }),
            ),
          },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DeadlineCreatePage);
    await fixture.whenStable();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');
    const page = fixture.componentInstance as unknown as {
      form: { patchValue(value: object): void };
      onCalculated(value: typeof suggestion): void;
      confirmed: { set(value: boolean): void };
      submit(): Promise<void>;
      store: { busy(): boolean };
    };
    page.form.patchValue({ type: 'Appeal', title: 'Appeal', assigneeUserId: 'user-1' });
    page.onCalculated(suggestion);
    page.confirmed.set(true);
    const saving = page.submit();
    expect(page.store.busy()).toBe(true);
    params.next(convertToParamMap({ caseId: 'case-2' }));
    await fixture.whenStable();
    expect(page.store.busy()).toBe(false);
    finish({ ok: true, value: { id: 'deadline-1' } });
    await saving;
    expect(navigate).not.toHaveBeenCalled();
    expect(page.store.busy()).toBe(false);
  });

  it('clears date confirmation when the suggestion changes', async () => {
    await TestBed.configureTestingModule({
      imports: [DeadlineCreatePage, translocoTesting()],
      providers: [
        provideRouter([]),
        {
          provide: DeadlinesApi,
          useValue: {
            options: vi.fn().mockResolvedValue({ ok: true, value: { members: [], teams: [] } }),
          },
        },
        { provide: ActivatedRoute, useValue: { queryParamMap: of({ get: () => null }) } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DeadlineCreatePage);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.componentInstance as unknown as {
      onCalculated(value: typeof suggestion | null): void;
      confirmed: { set(value: boolean): void; (): boolean };
    };
    page.onCalculated(suggestion);
    page.confirmed.set(true);
    expect(page.confirmed()).toBe(true);
    page.onCalculated(null);
    expect(page.confirmed()).toBe(false);
  });
});
