import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { translocoTesting } from '../../core/i18n/transloco-testing';
import { AccessStore } from '../../core/organization/access-store';
import { ControlApi } from './data/control-api';
import { ControlPage } from './control-page';

describe('ControlPage', () => {
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
