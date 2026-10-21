import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type {
  CalculateDeadlineQuery,
  CaseSummary,
  DeadlineCalculationResponse,
  DeadlineRule,
} from '../../../api/types.gen';
import { DeadlineCalculator } from '../components/deadline-calculator';
import { DeadlineEditorStore } from '../data/deadline-editor-store';
import { errorKey, formatDate } from '../data/deadline-format';

interface Suggestion {
  query: CalculateDeadlineQuery;
  result: DeadlineCalculationResponse;
}
@Component({
  selector: 'lf-deadline-create-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
    DeadlineCalculator,
  ],
  templateUrl: './deadline-create-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DeadlineEditorStore],
})
export class DeadlineCreatePage {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly store = inject(DeadlineEditorStore);
  protected readonly caseId = signal<string | null>(null);
  protected readonly suggestion = signal<Suggestion | null>(null);
  protected readonly confirmed = signal(false);
  protected readonly errorTranslationKey = errorKey;
  protected readonly date = formatDate;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    type: ['', [Validators.required, Validators.maxLength(80)]],
    title: ['', [Validators.required, Validators.maxLength(300)]],
    assigneeUserId: ['', Validators.required],
  });
  constructor() {
    void this.store.loadOptions();
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const id = params.get('caseId');
      this.caseId.set(id);
      this.suggestion.set(null);
      this.confirmed.set(false);
      if (id) void this.loadCase(id);
      else void this.store.changeCase(null);
    });
    this.form.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.confirmed.set(false));
  }
  protected searchCases(query: string): void {
    void this.store.searchCases(query);
  }
  private async loadCase(id: string): Promise<void> {
    await this.store.changeCase(id);
    if (this.caseId() === id && this.store.error()) this.caseId.set(null);
  }
  protected selectCase(item: CaseSummary): void {
    if (this.store.busy()) return;
    void this.store.selectCase(item);
    this.caseId.set(item.id);
    this.suggestion.set(null);
    this.confirmed.set(false);
  }
  protected onCalculated(value: Suggestion | null): void {
    this.suggestion.set(value);
    this.confirmed.set(false);
  }
  protected async submit(): Promise<void> {
    const suggestion = this.suggestion();
    const id = this.caseId();
    if (this.store.busy() || this.form.invalid || !suggestion || !this.confirmed() || !id) {
      this.form.markAllAsTouched();
      return;
    }
    const q = suggestion.query;
    const rule: DeadlineRule = {
      startKind: q.startKind,
      triggerDate: q.triggerDate,
      consultedOn: q.consultedOn,
      days: q.days,
      mode: q.mode,
      doubled: q.doubled,
      internalLeadDays: q.internalLeadDays,
    };
    const createdId = await this.store.save(id, {
      ...this.form.getRawValue(),
      rule,
      confirmedInternalDueOn: suggestion.result.internalDueOn,
      confirmedFatalDueOn: suggestion.result.fatalDueOn,
      confirmed: true,
      courtId: suggestion.result.courtId,
      stateCode: suggestion.result.stateCode,
      municipality: suggestion.result.municipality,
    });
    if (this.destroyRef.destroyed) return;
    if (!createdId) {
      if (this.store.error() === 'scheduling.calculation_changed') {
        this.suggestion.set(null);
        this.confirmed.set(false);
      }
      return;
    }
    await this.router.navigate(['/prazos', createdId]);
  }
}
