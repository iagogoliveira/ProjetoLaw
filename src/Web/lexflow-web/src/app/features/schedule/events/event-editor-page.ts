import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject } from '@angular/core';
import { untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AccessStore } from '../../../core/organization/access-store';
import type { CalendarEventInput, CalendarEventResponse } from '../../../api/types.gen';
import { localDateTime, utcDateTime } from '../data/schedule-format';
import { EventStore } from './event-store';
import { isEventRangeValid } from './event-validation';

@Component({
  selector: 'lf-event-editor-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [EventStore],
  templateUrl: './event-editor-page.html',
  styleUrls: ['../../../shared/ui/page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventEditorPage {
  protected readonly store = inject(EventStore);
  protected readonly access = inject(AccessStore);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private alive = true;
  private currentRouteId: string | null = null;
  private lastItemRevision = 0;
  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', Validators.maxLength(4000)],
    location: ['', Validators.maxLength(500)],
    startsAt: ['', Validators.required],
    endsAt: ['', Validators.required],
    assigneeUserId: ['', Validators.required],
    caseId: this.fb.control<string | null>(null),
  });
  protected readonly caseQuery = this.fb.nonNullable.control('');
  protected readonly cancelConfirmation = this.fb.nonNullable.control(false);
  protected endBeforeStart = false;
  protected timeConversionError = false;

  constructor() {
    effect(() => {
      const revision = this.store.itemRevision();
      if (revision > this.lastItemRevision) {
        this.lastItemRevision = revision;
        untracked(() => {
          const item = this.store.item();
          if (item && item.id === this.currentRouteId) this.hydrate(item);
        });
      }
    });
    this.route.paramMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      const id = params.get('id');
      this.currentRouteId = id;
      this.resetForm();
      if (this.access.can('schedule.events.read')) void this.store.loadOptions();
      if (id) void this.loadRouteEvent(id);
      else this.store.invalidate();
    });
    inject(DestroyRef).onDestroy(() => {
      this.alive = false;
      this.store.invalidate();
    });
  }

  protected get isEditing(): boolean {
    return this.currentRouteId !== null;
  }
  protected get isCancelled(): boolean {
    return this.store.item()?.status === 'cancelled';
  }
  protected get canEdit(): boolean {
    return this.access.can('schedule.events.manage') && !this.isCancelled;
  }
  protected readonly timeZoneLabel = 'America/Sao_Paulo';
  protected caseDisplay(folder: string, cnj: string | null): string {
    return cnj ? `${folder} · ${cnj}` : folder;
  }
  protected errorTranslation(
    code: string,
    source: 'event' | 'options' | 'cases' = 'event',
  ): string {
    if (
      [
        'authorization.forbidden',
        'auth.forbidden',
        'organization.not_a_member',
        'organization.mfa_required',
      ].includes(code)
    )
      return 'events.errors.access';
    if (code === 'scheduling.event_not_found') return 'events.errors.notFound';
    if (code === 'scheduling.event_closed') return 'events.errors.closed';
    if (code === 'scheduling.event_invalid' || code === 'scheduling.assignee_unavailable')
      return 'events.errors.reviewFields';
    return source === 'options'
      ? 'events.errors.options'
      : source === 'cases'
        ? 'events.errors.cases'
        : 'events.errors.generic';
  }

  protected async searchCases(): Promise<void> {
    await this.store.searchCases(this.caseQuery.value, this.access.can('cases.read'));
  }

  protected selectCase(id: string): void {
    const selected = this.store.cases().find((item) => item.id === id);
    this.form.controls.caseId.setValue(id || null);
    if (selected) this.store.setCaseLabel(selected.internalFolder);
  }

  protected async save(): Promise<void> {
    if (!this.canEdit || this.store.saving() || this.store.canceling() || this.store.loading())
      return;
    this.endBeforeStart = false;
    this.timeConversionError = false;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    if (!isEventRangeValid(value.startsAt, value.endsAt)) {
      this.endBeforeStart = true;
      return;
    }
    let startsAt: string;
    let endsAt: string;
    try {
      startsAt = utcDateTime(value.startsAt);
      endsAt = utcDateTime(value.endsAt);
    } catch {
      this.timeConversionError = true;
      return;
    }
    const input: CalendarEventInput = {
      title: value.title.trim(),
      description: value.description.trim() || null,
      location: value.location.trim() || null,
      startsAt,
      endsAt,
      assigneeUserId: value.assigneeUserId,
      caseId: this.isEditing ? (this.store.item()?.caseId ?? null) : value.caseId,
    };
    if (this.isEditing && this.currentRouteId) {
      await this.store.edit(this.currentRouteId, input);
    } else {
      const created = await this.store.create(input);
      if (created && this.alive) await this.router.navigate(['/agenda/compromissos', created.id]);
    }
  }

  protected async cancelEvent(): Promise<void> {
    const id = this.currentRouteId;
    if (
      !id ||
      !this.canEdit ||
      !this.cancelConfirmation.value ||
      this.store.saving() ||
      this.store.canceling()
    )
      return;
    await this.store.cancel(id);
    this.cancelConfirmation.setValue(false);
  }

  private async loadRouteEvent(id: string): Promise<void> {
    await this.store.load(id, this.access.can('cases.read'));
  }

  private hydrate(item: CalendarEventResponse): void {
    this.form.setValue({
      title: item.title,
      description: item.description ?? '',
      location: item.location ?? '',
      startsAt: localDateTime(item.startsAt),
      endsAt: localDateTime(item.endsAt),
      assigneeUserId: item.assigneeUserId,
      caseId: item.caseId,
    });
    this.endBeforeStart = false;
    this.timeConversionError = false;
    this.cancelConfirmation.setValue(false);
  }

  private resetForm(): void {
    this.form.reset({
      title: '',
      description: '',
      location: '',
      startsAt: '',
      endsAt: '',
      assigneeUserId: '',
      caseId: null,
    });
    this.caseQuery.setValue('');
    this.cancelConfirmation.setValue(false);
    this.endBeforeStart = false;
    this.timeConversionError = false;
  }
}
