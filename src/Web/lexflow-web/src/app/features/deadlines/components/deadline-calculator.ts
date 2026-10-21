import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnChanges,
  OnDestroy,
  output,
  SimpleChanges,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import type {
  CalculateDeadlineQuery,
  CountingMode,
  DeadlineStartKind,
} from '../../../api/types.gen';
import { errorKey, formatDate } from '../data/deadline-format';
import {
  DeadlineCalculationStore,
  type DeadlineSuggestion,
} from '../data/deadline-calculation-store';
const supportedTriggerDate: ValidatorFn = (control) =>
  typeof control.value === 'string' && control.value.slice(0, 4) > '9995'
    ? { dateOutOfRange: true }
    : null;

@Component({
  selector: 'lf-deadline-calculator',
  providers: [DeadlineCalculationStore],
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslocoDirective,
  ],
  templateUrl: './deadline-calculator.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeadlineCalculator implements OnChanges, OnDestroy {
  readonly caseId = input<string | null>(null);
  readonly triggerDate = input('');
  readonly calculated = output<DeadlineSuggestion | null>();
  protected readonly startKinds: DeadlineStartKind[] = [
    'eventDate',
    'publication',
    'djenAvailability',
    'portalIntimation',
  ];
  protected readonly modes: CountingMode[] = ['businessDays', 'calendarDays'];
  private readonly store = inject(DeadlineCalculationStore);
  protected readonly formatDate = formatDate;
  protected readonly errorKey = errorKey;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    startKind: ['eventDate' as DeadlineStartKind, Validators.required],
    triggerDate: ['', [Validators.required, supportedTriggerDate]],
    consultedOn: '',
    days: [15, [Validators.required, Validators.min(1), Validators.max(365)]],
    mode: ['businessDays' as CountingMode, Validators.required],
    doubled: false,
    internalLeadDays: [2, [Validators.required, Validators.min(0), Validators.max(30)]],
    stateCode: ['', [Validators.maxLength(2), Validators.pattern(/^[a-zA-Z]{2}$/)]],
    municipality: ['', Validators.maxLength(120)],
  });
  protected readonly suggestion = this.store.suggestion;
  protected readonly errorCode = this.store.errorCode;
  protected readonly busy = this.store.busy;
  private readonly subscription = this.form.valueChanges.subscribe(() => this.invalidate());

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['triggerDate']) {
      this.form.controls.triggerDate.setValue(this.triggerDate(), { emitEvent: false });
      this.invalidate();
    }
    if (changes['caseId']) this.invalidate();
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
    this.store.invalidate();
  }

  protected async calculate(): Promise<void> {
    if (this.busy()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const values = this.form.getRawValue();
    const query: CalculateDeadlineQuery = {
      caseId: this.caseId(),
      courtId: null,
      stateCode: values.stateCode.toUpperCase() || null,
      municipality: values.municipality || null,
      startKind: values.startKind,
      triggerDate: values.triggerDate,
      consultedOn: values.startKind === 'portalIntimation' ? values.consultedOn || null : null,
      days: values.days,
      mode: values.mode,
      doubled: values.doubled,
      internalLeadDays: values.internalLeadDays,
    };
    this.invalidate();
    await this.store.calculate(query);
  }

  protected applySuggestion(): void {
    const suggestion = this.suggestion();
    if (suggestion) this.calculated.emit(suggestion);
  }

  private invalidate(): void {
    this.store.invalidate();
    this.calculated.emit(null);
  }
}
