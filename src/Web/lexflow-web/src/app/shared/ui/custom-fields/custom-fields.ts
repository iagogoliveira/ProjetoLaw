import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import type { CustomFieldResponse, LookupTableResponse } from '../../../api/types.gen';

/** Pessoa que pode ser escolhida num campo do tipo usuário. */
export interface CustomFieldUser {
  userId: string;
  name: string;
}

/** Valor de um campo no formulário, no formato que a API espera (`custom_fields` da entidade). */
export type CustomFieldValue = string | number | string[];

const toNumber = (value: unknown): number | null =>
  value === null || value === undefined || value === '' ? null : Number(value);

/** Casas decimais além do permitido (número: as da definição; moeda: 2). */
export function decimalsValidator(decimals: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = toNumber(control.value);
    if (value === null || Number.isNaN(value)) {
      return null;
    }
    const factor = 10 ** decimals;
    return Math.abs(Math.round(value * factor) - value * factor) > 1e-6 ? { decimals: true } : null;
  };
}

/** Validadores do cliente equivalentes às regras do servidor (o servidor continua sendo quem decide). */
export function validatorsFor(field: CustomFieldResponse): ValidatorFn[] {
  const settings = field.settings;
  const validators: ValidatorFn[] = field.required ? [Validators.required] : [];
  switch (field.type) {
    case 'text':
      validators.push(Validators.maxLength(Number(settings.maxLength ?? 500)));
      break;
    case 'number':
    case 'currency':
      if (settings.minimum !== null && settings.minimum !== undefined) {
        validators.push(Validators.min(Number(settings.minimum)));
      }
      if (settings.maximum !== null && settings.maximum !== undefined) {
        validators.push(Validators.max(Number(settings.maximum)));
      }
      validators.push(
        decimalsValidator(field.type === 'currency' ? 2 : Number(settings.decimals ?? 0)),
      );
      break;
  }
  return validators;
}

/** Código `custom_fields.*` do primeiro erro do controle (validação local ou devolvida pela API). */
export function errorCode(control: AbstractControl | null): string | null {
  const errors = control?.errors;
  if (!errors) {
    return null;
  }
  if (typeof errors['server'] === 'string') {
    return errors['server'];
  }
  if (errors['required']) {
    return 'custom_fields.required';
  }
  if (errors['maxlength']) {
    return 'custom_fields.too_long';
  }
  if (errors['min']) {
    return 'custom_fields.below_minimum';
  }
  if (errors['max']) {
    return 'custom_fields.above_maximum';
  }
  if (errors['decimals']) {
    return 'custom_fields.too_many_decimals';
  }
  return 'custom_fields.invalid_value';
}

/** Valores do formulário prontos para a API: campos vazios ficam de fora e números viram number. */
export function customFieldValues(
  group: FormGroup,
  fields: readonly CustomFieldResponse[],
): Record<string, CustomFieldValue> {
  const values: Record<string, CustomFieldValue> = {};
  for (const field of fields) {
    const raw: unknown = group.get(field.key)?.value;
    if (
      raw === null ||
      raw === undefined ||
      raw === '' ||
      (Array.isArray(raw) && raw.length === 0)
    ) {
      continue;
    }
    values[field.key] =
      field.type === 'number' || field.type === 'currency'
        ? Number(raw)
        : (raw as string | string[]);
  }
  return values;
}

/**
 * Erros devolvidos pela API em `errors["customFields.{chave}"]` aplicados aos controles, para aparecerem no campo.
 */
export function applyServerErrors(
  group: FormGroup,
  errors: Record<string, readonly { code: string }[]> | undefined,
): void {
  for (const [name, problems] of Object.entries(errors ?? {})) {
    const key = name.startsWith('customFields.') ? name.slice('customFields.'.length) : null;
    const control = key ? group.get(key) : null;
    if (control && problems.length > 0) {
      control.setErrors({ server: problems[0].code });
      control.markAsTouched();
    }
  }
}

/**
 * Campos customizados de uma entidade (seção 5.2, item 6), montados a partir das definições do escritório. Os
 * controles entram no `group` recebido, um por chave, com os mesmos limites do servidor; só os campos ativos
 * aparecem, na ordem definida. Lista tira as opções ativas da tabela auxiliar do campo.
 */
@Component({
  selector: 'lf-custom-fields',
  imports: [
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
    TranslocoDirective,
  ],
  template: `
    <ng-container *transloco="let t" [formGroup]="group()">
      @for (field of visible(); track field.id) {
        <mat-form-field appearance="outline" class="field">
          <mat-label>{{ field.label }}</mat-label>
          @switch (field.type) {
            @case ('list') {
              <mat-select [formControlName]="field.key" [multiple]="field.settings.allowMultiple">
                @for (option of optionsOf(field); track option.id) {
                  <mat-option [value]="option.id">{{ option.name }}</mat-option>
                }
              </mat-select>
            }
            @case ('user') {
              <mat-select [formControlName]="field.key">
                @for (user of users(); track user.userId) {
                  <mat-option [value]="user.userId">{{ user.name }}</mat-option>
                }
              </mat-select>
            }
            @case ('date') {
              <input matInput type="date" [formControlName]="field.key" />
            }
            @case ('text') {
              <input
                matInput
                type="text"
                [formControlName]="field.key"
                [attr.maxlength]="field.settings.maxLength"
              />
            }
            @default {
              <input
                matInput
                type="number"
                inputmode="decimal"
                [formControlName]="field.key"
                [step]="field.type === 'currency' ? '0.01' : 'any'"
              />
              @if (field.type === 'currency') {
                <span matTextPrefix>R$&nbsp;</span>
              }
            }
          }
          @if (field.helpText) {
            <mat-hint>{{ field.helpText }}</mat-hint>
          }
          <mat-error>{{
            t('errors.' + (codeOf(field.key) ?? 'custom_fields.invalid_value'))
          }}</mat-error>
        </mat-form-field>
      }
    </ng-container>
  `,
  styles: `
    :host {
      display: grid;
      gap: var(--lf-space-sm);
    }

    .field {
      width: 100%;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomFields {
  /** Definições da entidade (`GET /api/v1/custom-fields?entity=…`). */
  readonly definitions = input.required<readonly CustomFieldResponse[]>();

  /** Formulário da entidade; recebe um controle por campo. */
  readonly group = input.required<FormGroup>();

  readonly lookupTables = input<readonly LookupTableResponse[]>([]);

  readonly users = input<readonly CustomFieldUser[]>([]);

  protected readonly visible = computed(() =>
    [...this.definitions()]
      .filter((f) => f.isActive)
      .sort((a, b) => Number(a.position) - Number(b.position)),
  );

  private readonly changeDetector = inject(ChangeDetectorRef);

  constructor() {
    // Erros e "tocado" mudam fora dos signals (validação no envio, erros da API): redesenha os campos.
    effect((onCleanup) => {
      const subscription = this.group().events.subscribe(() => this.changeDetector.markForCheck());
      onCleanup(() => subscription.unsubscribe());
    });

    // Mantém os controles do grupo iguais aos campos ativos, no mesmo grupo: definições chegam depois do formulário e
    // mudam (obrigatório, limites, desativação) sem que o formulário seja recriado.
    effect(() => {
      const group = this.group();
      const fields = this.visible();
      const keys = new Set(fields.map((f) => f.key));
      for (const field of fields) {
        const control = group.get(field.key);
        if (control) {
          control.setValidators(validatorsFor(field));
          control.updateValueAndValidity({ emitEvent: false });
        } else {
          group.addControl(field.key, new FormControl<unknown>(null, validatorsFor(field)));
        }
      }
      for (const key of Object.keys(group.controls).filter((k) => !keys.has(k))) {
        group.removeControl(key);
      }
    });
  }

  protected optionsOf(field: CustomFieldResponse) {
    const table = this.lookupTables().find((t) => t.id === field.settings.lookupTableId);
    return (table?.items ?? []).filter((i) => i.isActive);
  }

  protected codeOf(key: string): string | null {
    return errorCode(this.group().get(key));
  }
}
