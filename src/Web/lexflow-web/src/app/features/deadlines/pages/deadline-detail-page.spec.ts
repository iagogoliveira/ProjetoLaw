import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { DeadlineDetailResponse } from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { DeadlinesApi } from '../data/deadlines-api';
import { DeadlineDetailPage } from './deadline-detail-page';

const deadline: DeadlineDetailResponse = {
  id: 'deadline-1',
  caseId: 'case-1',
  publicationId: null,
  type: 'court',
  title: 'Prazo de manifestação',
  startOn: '2026-10-20',
  internalDueOn: '2026-10-25',
  fatalDueOn: '2026-10-27',
  assigneeUserId: 'user-1',
  status: 'inProgress',
  confirmedBy: 'user-1',
  confirmedAt: '2026-10-20T12:00:00Z',
  rule: null,
  calculation: null,
  requireReview: true,
  reviewedBy: null,
  reviewedAt: null,
  completionEvidence: null,
  missedReason: null,
  closedBy: null,
  closedAt: null,
  lastCalendarAlertAt: null,
};

describe('DeadlineDetailPage', () => {
  it('keeps completion blocked until an authorized review succeeds', async () => {
    const detail = vi.fn().mockResolvedValue({ ok: true, value: deadline });
    const review = vi
      .fn()
      .mockResolvedValue({ ok: false, code: 'scheduling.separate_reviewer_required' });
    await TestBed.configureTestingModule({
      imports: [DeadlineDetailPage, translocoTesting()],
      providers: [
        provideRouter([]),
        {
          provide: DeadlinesApi,
          useValue: {
            detail,
            review,
            complete: vi.fn(),
          },
        },
        {
          provide: AccessStore,
          useValue: {
            can: (permission: string) => permission !== 'cases.read',
            permissions: () => new Set(['schedule.deadlines.verify', 'schedule.deadlines.manage']),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => 'deadline-1' } },
            paramMap: of({ get: () => 'deadline-1' }),
          },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DeadlineDetailPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const page = fixture.componentInstance as unknown as {
      form: {
        patchValue(value: object): void;
        controls: { evidence: { value: string }; reason: { value: string } };
      };
      store: { item(): DeadlineDetailResponse | null; error(): string | null };
    };
    page.form.patchValue({ evidence: 'Protocol number 123', reason: 'No reason needed yet' });
    const element = fixture.nativeElement as HTMLElement;
    const complete = Array.from(element.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Cumprir prazo'),
    )!;
    expect(complete.disabled).toBe(true);
    const reviewButton = Array.from(element.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Conferir prazo'),
    )!;
    reviewButton.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(review).toHaveBeenCalledOnce();
    expect(detail).toHaveBeenCalledTimes(2);
    expect(page.store.item()?.id).toBe('deadline-1');
    expect(page.store.error()).toBe('scheduling.separate_reviewer_required');
    expect(page.form.controls.evidence.value).toBe('Protocol number 123');
    expect(page.form.controls.reason.value).toBe('No reason needed yet');
    expect((fixture.nativeElement as HTMLElement).querySelector('button[disabled]')).not.toBeNull();
  });
});
