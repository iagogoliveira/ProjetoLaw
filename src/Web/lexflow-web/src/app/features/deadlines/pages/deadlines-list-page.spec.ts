import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { DeadlineListPage } from '../../../api/types.gen';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import { AccessStore } from '../../../core/organization/access-store';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { DeadlinesApi } from '../data/deadlines-api';
import { DeadlinesListPage } from './deadlines-list-page';

describe('DeadlinesListPage', () => {
  it('hydrates its typed form from query parameters and applies edits back to the URL', async () => {
    const queryValues: Record<string, string | null> = {
      from: '2026-10-01',
      until: '2026-11-01',
      assigneeUserId: 'user-1',
      teamId: 'team-1',
      includeClosed: 'true',
      caseId: 'case-1',
    };
    const paramMap = {
      get: (key: string) => queryValues[key] ?? null,
      get keys() {
        return Object.keys(queryValues);
      },
    };
    const list = vi.fn().mockResolvedValue({
      ok: true,
      value: { today: '2026-10-21', items: [], nextCursor: null } satisfies DeadlineListPage,
    });
    const navigate = vi.fn().mockResolvedValue(true);
    await TestBed.configureTestingModule({
      imports: [DeadlinesListPage, translocoTesting()],
      providers: [
        { provide: DeadlinesApi, useValue: { list, options: vi.fn(), preference: vi.fn() } },
        {
          provide: AccessStore,
          useValue: { can: () => false, permissions: () => new Set<string>() },
        },
        {
          provide: ActivatedRoute,
          useValue: { queryParamMap: of(paramMap), snapshot: { queryParamMap: paramMap } },
        },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DeadlinesListPage);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.componentInstance as unknown as {
      filtersForm: {
        getRawValue(): Record<string, unknown>;
        patchValue(value: Record<string, unknown>): void;
      };
      applyFilters(): void;
    };
    expect(page.filtersForm.getRawValue()).toEqual({
      from: '2026-10-01',
      until: '2026-11-01',
      assigneeUserId: 'user-1',
      teamId: 'team-1',
      includeClosed: true,
    });
    page.filtersForm.patchValue({ from: '2026-10-02' });
    page.applyFilters();
    await fixture.whenStable();
    expect(navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({
        queryParams: {
          from: '2026-10-02',
          until: '2026-11-01',
          assigneeUserId: 'user-1',
          teamId: 'team-1',
          includeClosed: 'true',
          caseId: 'case-1',
        },
      }),
    );
    expect(list).toHaveBeenCalledWith(
      expect.objectContaining({ from: '2026-10-01', caseId: 'case-1' }),
      null,
    );
  });
});
