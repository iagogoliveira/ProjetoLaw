import { ChangeDetectionStrategy, Component, inject, input, OnInit, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import type { CaseOptionsResponse, RiskProbability } from '../../../../api/types.gen';
import { CanDirective } from '../../../../core/organization/can.directive';
import { RiskStore } from '../../data/risk-store';
import { MONEY_PATTERN, parseAmount } from '../../data/values-format';
import { ValueErrors } from './value-errors';

@Component({
  selector: 'lf-risk-tab',
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
  providers: [RiskStore],
  templateUrl: './risk-tab.html',
  styleUrl: '../../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RiskTab implements OnInit {
  readonly caseId = input.required<string>();
  readonly options = input<CaseOptionsResponse | null>(null);
  readonly closed = input(false);
  readonly changed = output<void>();
  protected readonly store = inject(RiskStore);
  protected readonly probabilities: RiskProbability[] = ['probable', 'possible', 'remote'];
  protected readonly form = inject(FormBuilder).nonNullable.group({
    claimId: null as string | null,
    probability: 'possible' as RiskProbability,
    estimatedLoss: ['', Validators.pattern(MONEY_PATTERN)],
    rationale: ['', Validators.maxLength(4000)],
  });
  protected readonly amount = (v: number | string | null) =>
    v === null
      ? '—'
      : new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(Number(v));
  protected readonly date = (v: string) => v.slice(0, 10).split('-').reverse().join('/');
  ngOnInit(): void {
    void this.store.load(this.caseId());
  }
  protected claimName(id: string): string {
    const claim = this.store.claims().find((c) => c.id === id);
    return (
      claim?.description ||
      this.options()?.claimTypes.find((c) => c.id === claim?.claimTypeId)?.name ||
      id
    );
  }
  protected memberName(id: string): string {
    return this.options()?.members.find((m) => m.userId === id)?.name ?? '—';
  }
  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    if (
      await this.store.assess({
        ...v,
        estimatedLoss: parseAmount(v.estimatedLoss),
        rationale: v.rationale.trim() || null,
      })
    ) {
      this.form.controls.rationale.reset();
      this.changed.emit();
    }
  }
}
