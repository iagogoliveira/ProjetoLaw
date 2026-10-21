import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import type { DeadlineCalculationResponse } from '../../../api/types.gen';
import { DeadlinesApi } from '../data/deadlines-api';
import { DeadlineCalculator } from './deadline-calculator';

const result: DeadlineCalculationResponse = {
  intimationOn: '2026-10-19',
  startsOn: '2026-10-20',
  fatalDueOn: '2026-11-10',
  internalDueOn: '2026-11-08',
  countedDays: 15,
  courtId: null,
  stateCode: 'SP',
  municipality: null,
  federalJustice: false,
  lines: [],
};

describe('DeadlineCalculator', () => {
  async function render(calculate: ReturnType<typeof vi.fn>) {
    await TestBed.configureTestingModule({
      imports: [DeadlineCalculator, translocoTesting()],
      providers: [{ provide: DeadlinesApi, useValue: { calculate } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(DeadlineCalculator);
    fixture.componentRef.setInput('caseId', 'case-1');
    fixture.componentRef.setInput('triggerDate', '2026-10-19');
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('ignores a late response after the query form changes', async () => {
    let resolve!: (value: { ok: true; value: DeadlineCalculationResponse }) => void;
    const calculate = vi.fn(
      () =>
        new Promise<{ ok: true; value: DeadlineCalculationResponse }>((done) => (resolve = done)),
    );
    const { fixture, element } = await render(calculate);
    const emitted = vi.fn();
    fixture.componentInstance.calculated.subscribe(emitted);
    element.querySelector<HTMLButtonElement>('[data-calculate-suggestion]')!.click();
    const date = element.querySelector<HTMLInputElement>('input[type=date]')!;
    date.value = '2026-10-20';
    date.dispatchEvent(new Event('input', { bubbles: true }));
    resolve({ ok: true, value: result });
    await fixture.whenStable();
    expect(element.textContent).not.toContain('10/11/2026');
    expect(emitted).toHaveBeenLastCalledWith(null);
  });

  it('emits a suggestion only when the user applies it', async () => {
    const calculate = vi.fn().mockResolvedValue({ ok: true, value: result });
    const { fixture, element } = await render(calculate);
    const emitted = vi.fn();
    fixture.componentInstance.calculated.subscribe(emitted);
    element.querySelector<HTMLButtonElement>('[data-calculate-suggestion]')!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(emitted).not.toHaveBeenCalledWith(expect.objectContaining({ result }));
    expect(element.textContent).toContain('10/11/2026');
    const apply = element.querySelector<HTMLButtonElement>('[data-apply-suggestion]')!;
    apply.click();
    expect(emitted).toHaveBeenLastCalledWith(expect.objectContaining({ result }));
    expect(calculate).toHaveBeenCalledWith(expect.objectContaining({ caseId: 'case-1' }));
  });
});
