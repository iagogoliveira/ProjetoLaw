import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AccessStore } from '../../core/organization/access-store';
import { ControlStore, type ControlFilters } from './data/control-store';

@Component({
  selector: 'lf-control-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [ControlStore],
  templateUrl: './control-page.html',
  styleUrls: ['../../shared/ui/page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ControlPage {
  protected readonly store = inject(ControlStore);
  protected readonly access = inject(AccessStore);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    from: ['', Validators.required],
    until: ['', Validators.required],
    idleDays: [30, [Validators.required, Validators.min(1), Validators.max(3650)]],
  });
  protected readonly saveName = inject(FormBuilder).nonNullable.control('', [
    Validators.required,
    Validators.maxLength(50),
  ]);
  private readonly router = inject(Router);
  protected readonly route = inject(ActivatedRoute);
  protected readonly availableSectionKinds = [
    'pendingPublications',
    'staleCases',
    'unassignedDeadlines',
    'deadlinesWithoutEvidence',
  ];
  protected readonly metricKinds = [
    'averageTriageMinutes',
    'retriagedPublications',
    'lateRegisteredDeadlines',
  ];
  protected readonly windowInvalid = signal(false);
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      const from = params.get('from');
      const until = params.get('until');
      const idleDays = Number(params.get('idleDays'));
      if (
        from &&
        until &&
        Number.isInteger(idleDays) &&
        idleDays >= 1 &&
        idleDays <= 3650 &&
        this.valid({ from, until, idleDays })
      ) {
        this.windowInvalid.set(false);
        this.form.setValue({ from, until, idleDays });
        void this.store.search({ from, until, idleDays });
      } else {
        const today = new Intl.DateTimeFormat('en-CA', {
          timeZone: 'America/Sao_Paulo',
          year: 'numeric',
          month: '2-digit',
          day: '2-digit',
        }).format(new Date());
        const fromDate = new Date(`${today}T00:00:00Z`);
        fromDate.setUTCDate(fromDate.getUTCDate() - 30);
        const untilDate = new Date(`${today}T00:00:00Z`);
        untilDate.setUTCDate(untilDate.getUTCDate() + 1);
        const filters = {
          from: fromDate.toISOString().slice(0, 10),
          until: untilDate.toISOString().slice(0, 10),
          idleDays: 30,
        };
        this.form.setValue(filters);
        void this.search(filters);
      }
    });
    if (this.access.can('identity.profile.read')) void this.store.loadSaved();
  }
  protected async apply(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const filters = this.form.getRawValue();
    this.windowInvalid.set(!this.valid(filters));
    if (this.windowInvalid()) return;
    await this.search(filters);
  }
  protected async saveCurrent(): Promise<void> {
    const name = this.saveName.getRawValue().trim();
    if (!name || this.saveName.invalid) {
      this.saveName.markAllAsTouched();
      return;
    }
    await this.store.save(name);
  }
  protected useSaved(filters: ControlFilters): void {
    this.form.setValue(filters);
    void this.search(filters);
  }
  protected async removeSaved(name: string): Promise<void> {
    await this.store.remove(name);
  }
  protected sectionTranslation(kind: string): string {
    return `control.sections.${kind}`;
  }
  protected metricTranslation(kind: string): string {
    return `control.metrics.${kind}`;
  }
  protected isPartial(count: number | string, shown: number): boolean {
    return Number(count) > shown;
  }
  protected itemLink(kind: string, id: string): string[] | null {
    if (kind === 'pendingPublications') return ['/publicacoes'];
    if (kind === 'staleCases') return ['/processos', id];
    if (kind === 'unassignedDeadlines' || kind === 'deadlinesWithoutEvidence')
      return ['/prazos', id];
    return null;
  }
  protected itemQuery(kind: string, id: string): Record<string, string> | null {
    return kind === 'pendingPublications' ? { publication: id } : null;
  }
  protected unitTranslation(unit: string): string {
    return `control.units.${unit}`;
  }
  protected reportErrorTranslation(code: string): string {
    if (code === 'control.invalid_query') return 'control.errors.invalidQuery';
    if (this.isAccessError(code)) return 'control.errors.access';
    return 'control.errors.generic';
  }
  protected preferenceErrorTranslation(code: string): string {
    if (code === 'control.filter_limit') return 'control.errors.filterLimit';
    if (this.isAccessError(code)) return 'control.errors.access';
    if (code === 'platform.preference_key_invalid' || code === 'platform.preference_value_invalid')
      return 'control.errors.preferenceInvalid';
    return 'control.errors.generic';
  }
  protected itemDate(value: string): string {
    return new Intl.DateTimeFormat('pt-BR', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: 'America/Sao_Paulo',
    }).format(new Date(value));
  }
  private valid(filters: ControlFilters): boolean {
    const datePattern = /^\d{4}-\d{2}-\d{2}$/;
    for (const value of [filters.from, filters.until]) {
      if (!datePattern.test(value)) return false;
      const date = new Date(`${value}T00:00:00Z`);
      if (Number.isNaN(date.valueOf()) || date.toISOString().slice(0, 10) !== value) return false;
    }
    const days =
      (Date.parse(`${filters.until}T00:00:00Z`) - Date.parse(`${filters.from}T00:00:00Z`)) /
      86400000;
    return days > 0 && days <= 366;
  }
  private isAccessError(code: string): boolean {
    return [
      'authorization.forbidden',
      'auth.forbidden',
      'organization.not_a_member',
      'organization.mfa_required',
    ].includes(code);
  }
  private async search(filters: ControlFilters): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: filters,
      queryParamsHandling: 'merge',
    });
  }
}
