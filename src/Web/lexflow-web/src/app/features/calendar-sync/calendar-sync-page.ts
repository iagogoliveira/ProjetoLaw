import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Temporal } from 'temporal-polyfill';
import type { CalendarProvider, CalendarResolution } from '../../api/types.gen';
import { AccessStore } from '../../core/organization/access-store';
import { OFFICE_ZONE, civilToday, formatInstant } from '../schedule/data/schedule-format';
import { CalendarSyncStore } from './data/calendar-sync-store';
import { isCalendarSyncWindowValid } from './data/calendar-sync-validation';

@Component({
  selector: 'lf-calendar-sync-page',
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
  providers: [CalendarSyncStore],
  templateUrl: './calendar-sync-page.html',
  styleUrls: ['../../shared/ui/page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CalendarSyncPage {
  protected readonly store = inject(CalendarSyncStore);
  protected readonly access = inject(AccessStore);
  protected readonly canManage = computed(() => this.access.can('identity.profile.manage'));
  protected readonly selectedConnection = computed(
    () => this.store.connections().find((item) => item.id === this.store.selectedId()) ?? null,
  );
  protected readonly form = inject(FormBuilder).nonNullable.group({
    provider: inject(FormBuilder).nonNullable.control<CalendarProvider>(
      'google',
      Validators.required,
    ),
    from: [civilToday(), Validators.required],
    until: [this.addDays(civilToday(), 365), Validators.required],
  });
  protected readonly disconnectConfirmation = inject(FormBuilder).nonNullable.control(false);
  protected readonly resolutionConfirmation = inject(FormBuilder).nonNullable.control(false);
  protected readonly officeZone = OFFICE_ZONE;
  protected readonly formatInstant = formatInstant;
  protected readonly dateWindowInvalid = signal(false);
  protected readonly resolvingId = signal<string | null>(null);
  constructor() {
    if (this.access.can('identity.profile.read')) void this.store.loadConnections();
  }
  protected editPeriod(id: string): void {
    const item = this.store.connections().find((connection) => connection.id === id);
    if (!item) return;
    this.store.select(id);
    this.form.setValue({ provider: item.provider, from: item.from, until: item.until });
    this.disconnectConfirmation.setValue(false);
  }
  protected selectConnection(id: string): void {
    this.store.select(id);
    this.resolvingId.set(null);
    this.resolutionConfirmation.setValue(false);
  }
  protected async savePeriod(): Promise<void> {
    if (!this.canManage() || this.store.busy()) return;
    const value = this.form.getRawValue();
    this.dateWindowInvalid.set(!isCalendarSyncWindowValid(value.from, value.until));
    if (this.form.invalid || this.dateWindowInvalid()) return;
    await this.store.connect({ provider: value.provider, from: value.from, until: value.until });
  }
  protected async disconnect(): Promise<void> {
    const id = this.store.selectedId();
    if (!id || !this.canManage() || !this.disconnectConfirmation.value) return;
    if (await this.store.disconnect(id)) this.disconnectConfirmation.setValue(false);
  }
  protected async queueSync(): Promise<void> {
    const id = this.store.selectedId();
    if (id && this.canManage()) await this.store.queueSync(id);
  }
  protected async refresh(): Promise<void> {
    await this.store.loadConnections();
    if (this.store.selectedId()) await this.store.loadPending(this.store.selectedId());
  }
  protected async resolve(id: string, resolution: CalendarResolution): Promise<void> {
    const connectionId = this.store.selectedId();
    if (
      !connectionId ||
      !this.canManage() ||
      this.resolvingId() !== id ||
      !this.resolutionConfirmation.value
    )
      return;
    const resolved = await this.store.resolve(connectionId, id, resolution);
    if (this.store.selectedId() !== connectionId || this.resolvingId() !== id) return;
    this.resolvingId.set(null);
    this.resolutionConfirmation.setValue(false);
    if (resolved === false) this.store.loadPending(connectionId);
  }
  protected reasonTranslation(reason: string): string {
    return ['conflict', 'externalDeletion', 'unsupported'].includes(reason)
      ? `calendarSync.pending.reasons.${reason}`
      : 'calendarSync.pending.reasons.generic';
  }
  protected errorTranslation(code: string): string {
    if (['authorization.forbidden', 'auth.forbidden', 'organization.not_a_member'].includes(code))
      return 'calendarSync.errors.access';
    if (code === 'scheduling.calendar_provider_unavailable')
      return 'calendarSync.errors.unavailable';
    if (code === 'scheduling.calendar_connection_invalid') return 'calendarSync.form.invalidWindow';
    if (code === 'scheduling.calendar_decision_stale') return 'calendarSync.pending.stale';
    return 'calendarSync.errors.generic';
  }
  protected kindTranslation(kind: string): string {
    return kind === 'deadline' ? 'calendarSync.items.deadline' : 'calendarSync.items.event';
  }
  protected canAccept(item: { canAcceptExternal: boolean }): boolean {
    return item.canAcceptExternal && this.access.can('schedule.events.manage');
  }
  protected localLink(item: { kind: string; localId: string | null }): string | null {
    if (!item.localId) return null;
    return item.kind === 'deadline'
      ? `/prazos/${item.localId}`
      : `/agenda/compromissos/${item.localId}`;
  }
  protected pendingActionLabel(
    item: { reason: string; localId: string | null },
    action: 'keep' | 'accept',
  ): string {
    if (action === 'keep' && item.reason === 'unsupported' && !item.localId)
      return 'calendarSync.pending.ignoreUnsupported';
    if (action === 'accept' && item.reason === 'externalDeletion')
      return 'calendarSync.pending.confirmCancellation';
    return action === 'keep'
      ? 'calendarSync.pending.keepLocal'
      : 'calendarSync.pending.acceptExternal';
  }
  protected setResolving(id: string): void {
    this.resolvingId.update((current) => (current === id ? null : id));
    this.resolutionConfirmation.setValue(false);
  }
  protected localDate(instant: string | null): string {
    return instant ? this.formatInstant(instant) : '—';
  }
  private addDays(date: string, days: number): string {
    return Temporal.PlainDate.from(date).add({ days }).toString();
  }
}
