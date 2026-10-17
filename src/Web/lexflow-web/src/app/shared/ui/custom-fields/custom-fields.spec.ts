import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import type { CustomFieldResponse, LookupTableResponse } from '../../../api/types.gen';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import {
  applyServerErrors,
  CustomFields,
  customFieldValues,
  errorCode,
  validatorsFor,
} from './custom-fields';

function field(overrides: Partial<CustomFieldResponse>): CustomFieldResponse {
  return {
    id: crypto.randomUUID(),
    entity: 'cases',
    key: 'campo',
    type: 'text',
    label: 'Campo',
    helpText: null,
    required: false,
    filterable: false,
    position: 0,
    isActive: true,
    settings: { allowMultiple: false },
    ...overrides,
  };
}

const phases: LookupTableResponse = {
  id: 'tabela-fases',
  name: 'Fases',
  systemKey: 'case_phases',
  isActive: true,
  items: [
    {
      id: 'conhecimento',
      tableId: 'tabela-fases',
      name: 'Conhecimento',
      position: 1,
      isActive: true,
    },
    { id: 'antiga', tableId: 'tabela-fases', name: 'Fase antiga', position: 2, isActive: false },
  ],
};

describe('CustomFields', () => {
  async function render(definitions: CustomFieldResponse[]) {
    await TestBed.configureTestingModule({
      imports: [CustomFields, translocoTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(CustomFields);
    const group = new FormGroup<Record<string, FormControl<unknown>>>({});
    fixture.componentRef.setInput('definitions', definitions);
    fixture.componentRef.setInput('group', group);
    fixture.componentRef.setInput('lookupTables', [phases]);
    fixture.componentRef.setInput('users', [{ userId: 'ana', name: 'Ana Souza' }]);
    await fixture.whenStable();
    return { fixture, group, element: fixture.nativeElement as HTMLElement };
  }

  it('renders the active fields in order and adds one control per key', async () => {
    const { group, element } = await render([
      field({ key: 'segundo', label: 'Segundo', position: 2 }),
      field({ key: 'primeiro', label: 'Primeiro', position: 1 }),
      field({ key: 'inativo', label: 'Inativo', isActive: false }),
    ]);

    const labels = [...element.querySelectorAll('mat-label')].map((l) => l.textContent?.trim());

    expect(labels).toEqual(['Primeiro', 'Segundo']);
    expect(Object.keys(group.controls).sort()).toEqual(['primeiro', 'segundo']);
  });

  it('shows the translated message of a required field left empty', async () => {
    const { fixture, group, element } = await render([
      field({ key: 'numero_pasta', label: 'Número da pasta', required: true }),
    ]);

    group.get('numero_pasta')?.markAsTouched();
    fixture.changeDetectorRef.markForCheck();
    await fixture.whenStable();

    expect(group.valid).toBe(false);
    expect(element.querySelector('mat-error')?.textContent?.trim()).toBe('Preencha este campo.');
  });

  it('redraws on its own when the form is submitted or the api returns errors', async () => {
    const { fixture, group, element } = await render([
      field({ key: 'numero_pasta', label: 'Número da pasta', required: true }),
    ]);

    group.markAllAsTouched();
    await fixture.whenStable();
    expect(element.querySelector('mat-error')?.textContent?.trim()).toBe('Preencha este campo.');

    group.get('numero_pasta')?.setValue('P-1');
    applyServerErrors(group, { 'customFields.numero_pasta': [{ code: 'custom_fields.too_long' }] });
    await fixture.whenStable();
    expect(element.querySelector('mat-error')?.textContent?.trim()).toBe(
      'Texto maior que o permitido.',
    );
  });

  it('keeps one control per active field in the same group as the definitions change', async () => {
    const { fixture, group } = await render([
      field({ key: 'a', required: false }),
      field({ key: 'b' }),
    ]);

    fixture.componentRef.setInput('definitions', [
      field({ key: 'a', required: true }),
      field({ key: 'c' }),
    ]);
    await fixture.whenStable();

    expect(Object.keys(group.controls).sort()).toEqual(['a', 'c']);
    expect(group.get('a')?.hasError('required')).toBe(true);
  });

  it('offers only the active options of the lookup table of a list field', async () => {
    const { fixture, element } = await render([
      field({
        key: 'fase',
        type: 'list',
        settings: { lookupTableId: 'tabela-fases', allowMultiple: false },
      }),
    ]);

    (element.querySelector('.mat-mdc-select-trigger') as HTMLElement).click();
    await fixture.whenStable();
    const options = [...document.querySelectorAll('mat-option')].map((o) => o.textContent?.trim());

    expect(options).toEqual(['Conhecimento']);
  });

  it('shows the error returned by the api on the field', async () => {
    const { fixture, group, element } = await render([field({ key: 'responsavel', type: 'user' })]);

    applyServerErrors(group, {
      'customFields.responsavel': [{ code: 'custom_fields.unknown_user' }],
    });
    fixture.changeDetectorRef.markForCheck();
    await fixture.whenStable();

    expect(element.querySelector('mat-error')?.textContent?.trim()).toBe(
      'Usuário não é membro do escritório.',
    );
  });
});

describe('custom field helpers', () => {
  function control(definition: CustomFieldResponse, value: unknown) {
    return new FormControl<unknown>(value, validatorsFor(definition));
  }

  it('mirror the server limits with the same codes', () => {
    const text = field({
      type: 'text',
      required: true,
      settings: { maxLength: 3, allowMultiple: false },
    });
    const number = field({
      type: 'number',
      settings: { minimum: 0, maximum: 10, decimals: 1, allowMultiple: false },
    });
    const currency = field({ type: 'currency' });

    expect(errorCode(control(text, ''))).toBe('custom_fields.required');
    expect(errorCode(control(text, 'abcd'))).toBe('custom_fields.too_long');
    expect(errorCode(control(number, -1))).toBe('custom_fields.below_minimum');
    expect(errorCode(control(number, 11))).toBe('custom_fields.above_maximum');
    expect(errorCode(control(number, 1.25))).toBe('custom_fields.too_many_decimals');
    expect(errorCode(control(number, 9.5))).toBeNull();
    expect(errorCode(control(currency, 10.005))).toBe('custom_fields.too_many_decimals');
    expect(errorCode(control(currency, 10.05))).toBeNull();
    expect(errorCode(null)).toBeNull();
  });

  it('builds the api values, skipping empty ones and converting numbers', () => {
    const fields = [
      field({ key: 'texto' }),
      field({ key: 'valor', type: 'currency' }),
      field({ key: 'vazio' }),
      field({
        key: 'pedidos',
        type: 'list',
        settings: { lookupTableId: 'x', allowMultiple: true },
      }),
      field({ key: 'nenhum', type: 'list', settings: { lookupTableId: 'x', allowMultiple: true } }),
    ];
    const group = new FormGroup({
      texto: new FormControl('Ana'),
      valor: new FormControl('1500.10'),
      vazio: new FormControl(''),
      pedidos: new FormControl(['a', 'b']),
      nenhum: new FormControl([]),
    });

    expect(customFieldValues(group, fields)).toEqual({
      texto: 'Ana',
      valor: 1500.1,
      pedidos: ['a', 'b'],
    });
  });

  it('ignores api errors of other fields', () => {
    const group = new FormGroup({ fase: new FormControl('x') });

    applyServerErrors(group, {
      'customFields.fase': [{ code: 'custom_fields.unknown_option' }],
      name: [{ code: 'other.code' }],
    });

    expect(errorCode(group.get('fase'))).toBe('custom_fields.unknown_option');
    expect(group.get('fase')?.touched).toBe(true);
  });
});
