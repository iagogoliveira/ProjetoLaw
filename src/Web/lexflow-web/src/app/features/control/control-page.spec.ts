import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { translocoTesting } from '../../core/i18n/transloco-testing';
import { AccessStore } from '../../core/organization/access-store';
import { ControlApi } from './data/control-api';
import { ControlPage } from './control-page';
import type { ControlReportResponse } from '../../api/types.gen';

describe('ControlPage', () => {
  it('keeps sample counts inside each metric definition, including metrics without samples', async () => {
    const report: ControlReportResponse = {
      asOf: '2026-10-22T12:00:00Z',
      from: '2026-10-01',
      until: '2026-11-01',
      idleDays: 30,
      sections: [],
      metrics: [
        { kind: 'averageTriageMinutes', value: null, sampleSize: 0, unit: 'minutes' },
        { kind: 'retriagedPublications', value: 2, sampleSize: 3, unit: 'publications' },
        { kind: 'lateRegisteredDeadlines', value: 1, sampleSize: 4, unit: 'count' },
      ],
    };
    await TestBed.configureTestingModule({
      imports: [ControlPage, translocoTesting()],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            queryParamMap: of(
              convertToParamMap({ from: report.from, until: report.until, idleDays: '30' }),
            ),
          },
        },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
        { provide: AccessStore, useValue: { can: () => false } },
        {
          provide: ControlApi,
          useValue: { report: vi.fn().mockResolvedValue({ ok: true, value: report }) },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(ControlPage);
    await fixture.whenStable();
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const groups = root.querySelectorAll('dl > div');
    expect(groups).toHaveLength(3);
    for (const group of groups) {
      expect(Array.from(group.children, (child) => child.tagName)).toEqual(['DT', 'DD']);
      expect(group.querySelector('dd small')?.textContent).toContain('Amostras:');
    }
    expect(groups[0]?.querySelector('dd')?.textContent).toContain('Sem amostras');
    expect(groups[1]?.querySelector('dd small')?.textContent).toContain('3');
  });

  it('maps API errors to localized messages without exposing internal codes', async () => {
    await TestBed.configureTestingModule({
      imports: [ControlPage, translocoTesting()],
      providers: [
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap({})) } },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
        { provide: AccessStore, useValue: { can: () => false } },
        { provide: ControlApi, useValue: { report: vi.fn(), preference: vi.fn() } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(ControlPage);
    await fixture.whenStable();
    const page = fixture.componentInstance as unknown as {
      reportErrorTranslation(code: string): string;
      preferenceErrorTranslation(code: string): string;
    };
    expect(page.reportErrorTranslation('unrecognized.http.code')).toBe('control.errors.generic');
    expect(page.preferenceErrorTranslation('platform.preference_value_invalid')).toBe(
      'control.errors.preferenceInvalid',
    );
  });
});
