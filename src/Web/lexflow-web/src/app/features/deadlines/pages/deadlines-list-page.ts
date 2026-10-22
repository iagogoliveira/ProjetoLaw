import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { AccessStore } from '../../../core/organization/access-store';
import { addCivilDays, deadlineUrgency, errorKey, formatDate } from '../data/deadline-format';
import type { DeadlineFilters } from '../data/deadlines-api';
import { DeadlinesListStore } from '../data/deadlines-list-store';

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

@Component({
  selector: 'lf-deadlines-list-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
    CanDirective,
  ],
  providers: [DeadlinesListStore],
  templateUrl: './deadlines-list-page.html',
  styleUrls: ['../../../shared/ui/page.scss', './deadlines-list-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeadlinesListPage {
  protected readonly store = inject(DeadlinesListStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly access = inject(AccessStore);
  private readonly formBuilder = inject(FormBuilder);
  protected readonly urgency = deadlineUrgency;
  protected readonly date = formatDate;
  protected readonly errorTranslationKey = errorKey;
  protected readonly filtersForm = this.formBuilder.nonNullable.group({
    from: ['', Validators.required],
    until: ['', Validators.required],
    assigneeUserId: this.formBuilder.control<string | null>(null),
    teamId: this.formBuilder.control<string | null>(null),
    includeClosed: false,
  });
  protected readonly saveName = this.formBuilder.nonNullable.control('', [
    Validators.required,
    Validators.maxLength(50),
  ]);

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      const filters = this.readFilters(params.get.bind(params));
      this.filtersForm.setValue({
        from: filters.from,
        until: filters.until,
        assigneeUserId: filters.assigneeUserId,
        teamId: filters.teamId,
        includeClosed: filters.includeClosed,
      });
      void this.store.search(filters);
    });
    if (this.access.can('cases.read')) void this.store.loadOptions();
    if (this.access.can('identity.profile.read')) void this.store.loadSaved();
  }

  protected applyFilters(): void {
    if (this.filtersForm.invalid) {
      this.filtersForm.markAllAsTouched();
      return;
    }
    void this.navigateWithFilters(this.filtersFromForm());
  }

  protected async saveCurrentFilter(): Promise<void> {
    const name = this.saveName.getRawValue().trim();
    if (this.saveName.invalid || !name) {
      this.saveName.markAsTouched();
      return;
    }
    await this.navigateWithFilters(this.filtersFromForm());
    await this.store.saveFilter(name);
  }

  protected async applySavedFilter(name: string): Promise<void> {
    const saved = this.store.saved().find((filter) => filter.name === name);
    if (saved) await this.navigateWithFilters(saved.filters);
  }

  protected removeSavedFilter(name: string): void {
    void this.store.removeFilter(name);
  }

  protected async navigateWithFilters(filters: DeadlineFilters): Promise<void> {
    const queryParams = this.queryParams(filters);
    const entries = Object.entries(queryParams).filter(
      ([, value]) => value !== null && value !== undefined,
    );
    const sameUrl =
      entries.every(([key, value]) => this.route.snapshot.queryParamMap.get(key) === value) &&
      entries.length === this.route.snapshot.queryParamMap.keys.length;
    if (sameUrl) {
      await this.store.search(filters);
      return;
    }
    const navigated = await this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      replaceUrl: true,
    });
    if (!navigated) await this.store.search(filters);
  }

  private filtersFromForm(): DeadlineFilters {
    return {
      ...this.filtersForm.getRawValue(),
      caseId: this.route.snapshot.queryParamMap.get('caseId'),
    };
  }

  private readFilters(get: (key: string) => string | null): DeadlineFilters {
    const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(
      new Date(),
    );
    const from = get('from');
    const until = get('until');
    return {
      from: from && DATE_PATTERN.test(from) ? from : addCivilDays(today, -30),
      until: until && DATE_PATTERN.test(until) ? until : addCivilDays(today, 31),
      assigneeUserId: get('assigneeUserId'),
      teamId: get('teamId'),
      includeClosed: get('includeClosed') === 'true',
      caseId: get('caseId'),
    };
  }

  private queryParams(filters: DeadlineFilters): Params {
    return {
      from: filters.from,
      until: filters.until,
      assigneeUserId: filters.assigneeUserId,
      teamId: filters.teamId,
      includeClosed: filters.includeClosed ? 'true' : 'false',
      caseId: filters.caseId ?? null,
    };
  }
}
