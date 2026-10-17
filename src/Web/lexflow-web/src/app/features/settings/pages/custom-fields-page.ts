import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import type { CustomFieldType } from '../../../api/types.gen';
import { CanDirective } from '../../../core/organization/can.directive';
import {
  applyServerErrors,
  CustomFields,
  customFieldValues,
} from '../../../shared/ui/custom-fields/custom-fields';
import { CUSTOM_FIELD_ENTITIES, CustomFieldsStore } from '../data/custom-fields-store';

const TYPES: readonly CustomFieldType[] = ['text', 'number', 'date', 'list', 'currency', 'user'];

/** Chave `snake_case` sugerida a partir do rótulo (sem acentos), como o servidor exige. */
export function keyFromLabel(label: string): string {
  const key = label
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '');
  return /^[a-z]/.test(key) ? key.slice(0, 50) : `campo_${key}`.slice(0, 50);
}

/**
 * Campos customizados por entidade (RF-03): criar, tornar obrigatório, desativar; tabelas auxiliares e itens; e o
 * formulário de teste, que monta os campos com `lf-custom-fields` e valida no servidor.
 */
@Component({
  selector: 'lf-custom-fields-page',
  imports: [
    CanDirective,
    CustomFields,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
    TranslocoDirective,
  ],
  providers: [CustomFieldsStore],
  templateUrl: './custom-fields-page.html',
  styleUrl: './settings-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomFieldsPage implements OnInit {
  protected readonly store = inject(CustomFieldsStore);
  protected readonly entities = CUSTOM_FIELD_ENTITIES;
  protected readonly types = TYPES;

  protected readonly field = new FormGroup({
    label: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    type: new FormControl<CustomFieldType>('text', { nonNullable: true }),
    required: new FormControl(false, { nonNullable: true }),
    lookupTableId: new FormControl<string | null>(null),
    allowMultiple: new FormControl(false, { nonNullable: true }),
  });

  protected readonly newTable = new FormControl('', { nonNullable: true });
  protected readonly newItems = signal<Record<string, string>>({});

  /** Formulário de teste: o `lf-custom-fields` mantém nele um controle por campo ativo. */
  protected readonly testForm = new FormGroup({});

  ngOnInit(): Promise<void> {
    return this.store.load();
  }

  protected async selectEntity(entity: string): Promise<void> {
    this.testForm.reset();
    await this.store.selectEntity(entity);
  }

  protected async createField(): Promise<void> {
    if (this.field.invalid) {
      this.field.markAllAsTouched();
      return;
    }

    const v = this.field.getRawValue();
    const created = await this.store.createField({
      key: keyFromLabel(v.label),
      type: v.type,
      label: v.label.trim(),
      helpText: null,
      required: v.required,
      filterable: false,
      position: this.store.fields().length + 1,
      settings:
        v.type === 'list'
          ? { lookupTableId: v.lookupTableId, allowMultiple: v.allowMultiple }
          : null,
    });
    if (created) {
      this.field.reset();
      this.testForm.reset();
    }
  }

  protected async toggleField(
    id: string,
    changes: { required?: boolean; isActive?: boolean },
  ): Promise<void> {
    const field = this.store.fields().find((f) => f.id === id);
    if (field && (await this.store.setFieldState(field, changes))) {
      this.testForm.reset();
    }
  }

  protected async createTable(): Promise<void> {
    if (await this.store.createTable(this.newTable.value.trim())) {
      this.newTable.reset();
    }
  }

  protected itemDraft(tableId: string): string {
    return this.newItems()[tableId] ?? '';
  }

  protected setItemDraft(tableId: string, value: string): void {
    this.newItems.update((drafts) => ({ ...drafts, [tableId]: value }));
  }

  protected async addItem(tableId: string): Promise<void> {
    if (await this.store.addItem(tableId, this.itemDraft(tableId).trim())) {
      this.setItemDraft(tableId, '');
    }
  }

  protected async runTest(): Promise<void> {
    const form = this.testForm;
    form.markAllAsTouched();
    const result = await this.store.validate(customFieldValues(form, this.store.fields()));
    if (result && !result.valid) {
      applyServerErrors(form, result.errors);
    }
  }
}
