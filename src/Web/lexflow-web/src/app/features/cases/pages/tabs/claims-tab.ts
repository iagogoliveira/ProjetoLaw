import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import type {
  CaseOptionsResponse,
  ClaimCorrectionIndex,
  ClaimResponse,
} from '../../../../api/types.gen';
import { CanDirective } from '../../../../core/organization/can.directive';
import { ClaimsStore } from '../../data/claims-store';
import {
  CASE_INSTANCES,
  editAmount,
  MONEY_PATTERN,
  parseAmount,
  today,
} from '../../data/values-format';
import { ValueErrors } from './value-errors';

@Component({
  selector: 'lf-claims-tab',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslocoDirective,
    CanDirective,
    ValueErrors,
  ],
  providers: [ClaimsStore],
  templateUrl: './claims-tab.html',
  styleUrl: '../../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClaimsTab implements OnInit {
  readonly caseId = input.required<string>();
  readonly options = input<CaseOptionsResponse | null>(null);
  readonly closed = input(false);
  readonly changed = output<void>();
  protected readonly store = inject(ClaimsStore);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly updateTo = this.fb.control(today(), Validators.required);
  protected readonly editing = signal<string | null | undefined>(undefined);
  protected readonly deleting = signal<string | null>(null);
  protected readonly indexes: ClaimCorrectionIndex[] = ['ipcaE', 'inpc', 'selic', 'tr'];
  protected readonly instances = CASE_INSTANCES;
  protected readonly form = this.fb.group({
    claimTypeId: ['', Validators.required],
    description: ['', Validators.maxLength(4000)],
    index: null as ClaimCorrectionIndex | null,
    correctFrom: '',
    interestFrom: '',
    monthlyInterestPercent: ['', Validators.pattern(/^\d+(?:,\d{1,4})?$/)],
    valuations: this.fb.array(
      CASE_INSTANCES.map((instance) =>
        this.fb.group({
          instance,
          requested: ['', Validators.pattern(MONEY_PATTERN)],
          estimated: ['', Validators.pattern(MONEY_PATTERN)],
          awarded: ['', Validators.pattern(MONEY_PATTERN)],
        }),
      ),
    ),
  });
  protected readonly amount = (value: number | string | null) =>
    value === null
      ? '—'
      : new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(
          Number(value),
        );
  protected readonly date = (value: string) => value.slice(0, 10).split('-').reverse().join('/');

  ngOnInit(): void {
    void this.reload();
  }
  protected reload(): Promise<void> {
    return this.store.load(this.caseId(), this.updateTo.value || null);
  }
  protected claimName(id: string): string {
    return this.options()?.claimTypes.find((c) => c.id === id)?.name ?? '—';
  }
  protected memberName(id: string): string {
    return this.options()?.members.find((m) => m.userId === id)?.name ?? '—';
  }

  protected edit(claim: ClaimResponse | null): void {
    this.form.reset();
    this.form.controls.valuations.controls.forEach((row, i) =>
      row.controls.instance.setValue(CASE_INSTANCES[i]),
    );
    this.editing.set(claim?.id ?? null);
    if (!claim) return;
    this.form.patchValue({
      claimTypeId: claim.claimTypeId,
      description: claim.description ?? '',
      index: claim.correction?.index ?? null,
      correctFrom: claim.correction?.correctFrom ?? '',
      interestFrom: claim.correction?.interestFrom ?? '',
      monthlyInterestPercent:
        claim.correction?.monthlyInterestPercent == null
          ? ''
          : String(claim.correction.monthlyInterestPercent).replace('.', ','),
    });
    this.form.controls.valuations.controls.forEach((row) => {
      const v = claim.valuations.find((v) => v.instance === row.controls.instance.value);
      row.patchValue({
        requested: editAmount(v?.requested ?? null),
        estimated: editAmount(v?.estimated ?? null),
        awarded: editAmount(v?.awarded ?? null),
      });
    });
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    const v = this.form.getRawValue();
    if (this.form.invalid || (v.index && !v.correctFrom) || this.editing() === undefined) return;
    const done = await this.store.save(this.editing() ?? null, {
      claimTypeId: v.claimTypeId,
      description: v.description.trim() || null,
      correction: v.index
        ? {
            index: v.index,
            correctFrom: v.correctFrom,
            interestFrom: v.index === 'selic' ? null : v.interestFrom || null,
            monthlyInterestPercent:
              v.index === 'selic' || !v.monthlyInterestPercent
                ? null
                : Number(v.monthlyInterestPercent.replace(',', '.')),
          }
        : null,
      valuations: v.valuations
        .filter((row) => row.requested || row.estimated || row.awarded)
        .map((row) => ({
          instance: row.instance,
          requested: parseAmount(row.requested),
          estimated: parseAmount(row.estimated),
          awarded: parseAmount(row.awarded),
        })),
    });
    if (done) {
      this.editing.set(undefined);
      this.changed.emit();
    }
  }

  protected async remove(): Promise<void> {
    const id = this.deleting();
    if (id && (await this.store.remove(id))) {
      this.deleting.set(null);
      this.changed.emit();
    }
  }
}
