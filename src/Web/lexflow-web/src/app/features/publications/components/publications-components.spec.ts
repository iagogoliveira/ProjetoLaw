import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AccessStore } from '../../../core/organization/access-store';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import { PublicationsStore } from '../data/publications-store';
import { PublicationsLiveStore } from '../data/publications-live-store';
import type { DeadlineCalculationResponse } from '../../../api/types.gen';
import { DeadlinesApi } from '../../deadlines/data/deadlines-api';
import { TriageForm } from './triage-form';
import { PublicationsPage } from '../pages/publications-page';

describe('Publication pages', () => {
  async function render(permissions: string[] = []) {
    const selected = signal({
      id: 'p',
      sourceId: 's',
      journal: 'DJE',
      text: '<img src=x onerror=alert(1)> 2238058-98.2025.8.26.8434',
      publishedOn: '2026-10-19',
      availableOn: '2026-10-19',
      receivedAt: '2026-10-19T15:00:00Z',
      status: 'pending',
      legalCaseId: null,
      extractedCnj: null,
    });
    const store = {
      selected,
      options: signal({ members: [], cases: [] }),
      items: signal([
        {
          id: 'candidate',
          journal: 'DJE',
          status: 'pending',
          publishedOn: '2026-10-19',
          extractedCnj: null,
        },
      ]),
      nextCursor: signal(null),
      loading: signal(false),
      busy: signal(false),
      selecting: signal(false),
      errorCode: signal(null),
      notice: signal(false),
      select: vi.fn(),
      search: vi.fn().mockResolvedValue(undefined),
      loadOptions: vi.fn(),
      news: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [PublicationsPage, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap({})) } },
        {
          provide: PublicationsLiveStore,
          useValue: { revision: signal(0), refreshCount: vi.fn() },
        },
        {
          provide: AccessStore,
          useValue: {
            permissions: signal(new Set(permissions)),
            can: (permission: string) => permissions.includes(permission),
          },
        },
      ],
    })
      .overrideComponent(PublicationsPage, {
        set: { providers: [{ provide: PublicationsStore, useValue: store }] },
      })
      .compileComponents();
    const fixture = TestBed.createComponent(PublicationsPage);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement, store };
  }
  it('renders publication text as text and highlights the CNJ without HTML execution', async () => {
    const { element } = await render(['publications.read']);
    expect(element.querySelector('.publication-text img')).toBeNull();
    expect(element.querySelector('.publication-text')?.textContent).toContain('<img');
    expect(element.querySelector('mark')?.textContent).toBe('2238058-98.2025.8.26.8434');
  });
  it('does not expose triage, distribution, sources or closing controls to a reader', async () => {
    const { element } = await render(['publications.read']);
    expect(element.textContent).not.toContain('Confirmar e salvar triagem');
    expect(element.textContent).not.toContain('Salvar distribuição');
    expect(element.querySelector('a[href="/publicacoes/fontes"]')).toBeNull();
    expect(element.querySelector('a[href="/publicacoes/fechamento"]')).toBeNull();
  });
  it('single-key shortcuts can be disabled and ignore editable controls', async () => {
    const { fixture, element, store } = await render(['publications.read']);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const checkbox = element.querySelector<HTMLInputElement>('input[type=checkbox]')!;
    checkbox.click();
    await fixture.whenStable();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j', bubbles: true }));
    expect(store.select).toHaveBeenCalledTimes(1);
    expect(navigate).not.toHaveBeenCalled();
    checkbox.click();
    await fixture.whenStable();
    const input = document.createElement('input');
    element.append(input);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'j', bubbles: true }));
    expect(store.select).toHaveBeenCalledTimes(1);
    expect(navigate).not.toHaveBeenCalled();
  });
});

describe('Triage draft lifecycle', () => {
  async function render() {
    const publication = {
      id: 'p',
      sourceId: 's',
      journal: 'DJE',
      text: 'Notice',
      publishedOn: '2026-10-19',
      availableOn: '2026-10-19',
      receivedAt: '2026-10-19T15:00:00Z',
      status: 'pending',
      legalCaseId: null,
      extractedCnj: null,
    };
    await TestBed.configureTestingModule({
      imports: [TriageForm, translocoTesting()],
      providers: [
        {
          provide: DeadlinesApi,
          useValue: {
            calculate: vi.fn().mockResolvedValue({
              ok: true,
              value: {
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
              } satisfies DeadlineCalculationResponse,
            }),
          },
        },
        { provide: PublicationsStore, useValue: { busy: signal(false) } },
        {
          provide: AccessStore,
          useValue: { permissions: signal(new Set(['publications.triage'])), can: () => true },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(TriageForm);
    fixture.componentRef.setInput('publication', publication);
    fixture.componentRef.setInput('options', { members: [], cases: [] });
    await fixture.whenStable();
    return { fixture, publication, element: fixture.nativeElement as HTMLElement };
  }
  it('preserves the draft when case options or the same publication assignment change', async () => {
    const { fixture, publication, element } = await render();
    const reason = element.querySelector<HTMLTextAreaElement>('textarea[formcontrolname=reason]')!;
    reason.value = 'Draft reason';
    reason.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.componentRef.setInput('options', {
      members: [{ userId: 'u', name: 'Member' }],
      cases: [],
    });
    await fixture.whenStable();
    expect(reason.value).toBe('Draft reason');
    fixture.componentRef.setInput('publication', { ...publication, assigneeUserId: 'u' });
    await fixture.whenStable();
    expect(reason.value).toBe('Draft reason');
    fixture.componentRef.setInput('publication', { ...publication, id: 'new' });
    await fixture.whenStable();
    expect(reason.value).toBe('');
  });
  it('does not emit a decision without explicit human confirmation', async () => {
    const { fixture, element } = await render();
    const emitted = vi.fn();
    fixture.componentInstance.triageRequested.subscribe(emitted);
    element
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(emitted).not.toHaveBeenCalled();
  });
  it('offers a calculator for a linked deadline and applies dates without confirming the triage', async () => {
    const { fixture, publication, element } = await render();
    const component = fixture.componentInstance as unknown as { form: FormGroup };
    component.form.patchValue({
      startOn: '2026-10-20',
      internalDueOn: '2026-11-08',
      fatalDueOn: '2026-11-10',
      confirmed: true,
    });
    fixture.componentRef.setInput('publication', { ...publication, legalCaseId: 'case-1' });
    await fixture.whenStable();
    component.form.controls['action'].setValue('deadline');
    fixture.detectChanges();
    await fixture.whenStable();
    const calculator = element.querySelector('lf-deadline-calculator')!;
    expect(calculator).not.toBeNull();
    expect(component.form.getRawValue()).toMatchObject({
      startOn: '2026-10-20',
      internalDueOn: '2026-11-08',
      fatalDueOn: '2026-11-10',
      confirmed: false,
    });
    calculator.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    const buttons = Array.from(calculator.querySelectorAll('button'));
    buttons.at(-1)!.click();
    expect(component.form.getRawValue()).toMatchObject({
      startOn: '2026-10-20',
      internalDueOn: '2026-11-08',
      fatalDueOn: '2026-11-10',
      confirmed: false,
    });
    component.form.controls['internalDueOn'].setValue('2026-11-07');
    const trigger = calculator.querySelector<HTMLInputElement>('input[type=date]')!;
    trigger.value = '2026-10-21';
    trigger.dispatchEvent(new Event('input', { bubbles: true }));
    expect(component.form.getRawValue()).toMatchObject({
      startOn: '',
      internalDueOn: '2026-11-07',
      fatalDueOn: '',
      confirmed: false,
    });
  });
});
