import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { FullCalendarComponent, FullCalendarModule } from '@fullcalendar/angular';
import dayGridPlugin from '@fullcalendar/angular/daygrid';
import timeGridPlugin from '@fullcalendar/angular/timegrid';
import classicTheme from 'fullcalendar/themes/classic';
import ptLocale from 'fullcalendar/locales/pt-br';
import type { CalendarOptions } from 'fullcalendar';
import { Temporal } from 'temporal-polyfill';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import type { AgendaItem } from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { CanDirective } from '../../../core/organization/can.directive';
import { AgendaStore } from '../data/agenda-store';
import { AgendaPreferencesStore } from '../data/agenda-preferences-store';
import {
  agendaLink,
  agendaWindow,
  civilToday,
  formatInstant,
  OFFICE_ZONE,
  validWindow,
} from '../data/schedule-format';
import { addCivilDays } from '../../deadlines/data/deadline-format';

@Component({
  selector: 'lf-agenda-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslocoDirective,
    CanDirective,
    FullCalendarModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  providers: [AgendaStore, AgendaPreferencesStore],
  templateUrl: './agenda-page.html',
  styleUrls: ['../../../shared/ui/page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgendaPage {
  protected readonly store = inject(AgendaStore);
  protected readonly saved = inject(AgendaPreferencesStore);
  protected readonly access = inject(AccessStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslocoService);
  private readonly language = toSignal(this.i18n.langChanges$, {
    initialValue: this.i18n.getActiveLang(),
  });
  private readonly fb = inject(FormBuilder);
  private readonly calendar = viewChild(FullCalendarComponent);
  protected readonly link = agendaLink;
  protected readonly instant = formatInstant;
  protected readonly selected = signal<AgendaItem[]>([]);
  protected readonly form = this.fb.group({
    from: this.fb.nonNullable.control(civilToday(), Validators.required),
    until: this.fb.nonNullable.control(addCivilDays(civilToday(), 7), Validators.required),
    assigneeUserId: this.fb.control<string | null>(null),
    teamId: this.fb.control<string | null>(null),
    includeClosed: this.fb.nonNullable.control(false),
  });
  protected readonly filterName = this.fb.nonNullable.control('', [
    Validators.required,
    Validators.maxLength(50),
  ]);
  protected readonly assignee = this.fb.nonNullable.control('', Validators.required);
  protected readonly view = signal('timeGridWeek');
  protected readonly anchor = signal(civilToday());
  protected readonly dateInput = this.fb.nonNullable.control(civilToday(), Validators.required);
  protected readonly options = computed<CalendarOptions>(() => ({
    plugins: [dayGridPlugin, timeGridPlugin, classicTheme],
    initialView: this.view(),
    initialDate: this.anchor(),
    timeZone: OFFICE_ZONE,
    firstDay: 1,
    locale: this.language() === 'pt-BR' ? ptLocale : 'en',
    headerToolbar: false,
    height: 'auto',
    editable: false,
    showNonCurrentDates: false,
    eventClick: (info) => {
      if (info.event.url) {
        info.jsEvent.preventDefault();
        void this.router.navigateByUrl(info.event.url);
      }
    },
    events: this.store.items().map((item) => ({
      id: `${item.kind}:${item.id}`,
      title: this.i18n.translate(`schedule.kinds.${item.kind}`) + ': ' + this.itemTitle(item),
      start: item.allDay ? item.startsAt.slice(0, 10) : item.startsAt,
      end: item.allDay ? undefined : (item.endsAt ?? undefined),
      allDay: item.allDay,
      url: agendaLink(item) ?? undefined,
    })),
  }));
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      const requested = params.get('date') ?? params.get('from') ?? civilToday();
      let day = civilToday();
      try {
        day = Temporal.PlainDate.from(requested).toString();
      } catch {
        /* Invalid links use today's view. */
      }
      const view = params.get('view') ?? 'timeGridWeek';
      this.view.set(
        ['dayGridMonth', 'timeGridWeek', 'timeGridDay'].includes(view) ? view : 'timeGridWeek',
      );
      this.anchor.set(day);
      this.dateInput.setValue(day);
      const filters = {
        ...agendaWindow(day, this.view()),
        assigneeUserId: params.get('assigneeUserId'),
        teamId: params.get('teamId'),
        includeClosed: params.get('includeClosed') === 'true',
      };
      this.form.setValue(filters);
      this.selected.set([]);
      void this.store.search(filters);
      const api = this.calendar()?.getApi();
      if (api) {
        if (api.view.type !== this.view()) api.changeView(this.view(), day);
        else api.gotoDate(day);
      }
    });
    void this.store.optionsLoad();
    if (this.access.can('identity.profile.read')) void this.saved.load();
  }
  protected apply(): void {
    const filters = this.form.getRawValue();
    if (this.form.invalid || this.dateInput.invalid || !validWindow(filters.from, filters.until))
      return;
    try {
      Temporal.PlainDate.from(this.dateInput.value);
    } catch {
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { ...filters, date: this.dateInput.value, view: this.view() },
    });
  }
  protected changeView(view: string): void {
    this.view.set(view);
    this.apply();
  }
  protected move(direction: number): void {
    const day = Temporal.PlainDate.from(this.anchor());
    const date =
      direction === 0
        ? civilToday()
        : day
            .add(
              this.view() === 'dayGridMonth'
                ? { months: direction }
                : { days: direction * (this.view() === 'timeGridWeek' ? 7 : 1) },
            )
            .toString();
    this.dateInput.setValue(date);
    this.apply();
  }
  protected canSelect(item: AgendaItem): boolean {
    const permission =
      item.kind === 'deadline'
        ? 'schedule.deadlines.manage'
        : item.kind === 'task'
          ? 'schedule.tasks.manage'
          : 'schedule.events.manage';
    return (
      item.kind !== 'hearing' &&
      !['completed', 'cancelled', 'missed'].includes(item.status) &&
      this.access.can(permission)
    );
  }
  protected itemTitle(item: AgendaItem): string {
    return item.kind === 'hearing'
      ? this.i18n.translate(`cases.hearings.types.${item.title}`)
      : item.title;
  }
  protected memberName(id: string): string | null {
    return this.store.options()?.members.find((member) => member.userId === id)?.name ?? null;
  }
  protected toggle(item: AgendaItem, checked: boolean): void {
    this.selected.update((rows) =>
      checked
        ? [...rows, item]
        : rows.filter((row) => row.id !== item.id || row.kind !== item.kind),
    );
  }
  protected isSelected(item: AgendaItem): boolean {
    return this.selected().some((row) => row.id === item.id && row.kind === item.kind);
  }
  protected async redistribute(): Promise<void> {
    if (this.assignee.invalid) return;
    await this.store.redistribute(this.selected(), this.assignee.value);
    this.selected.set([]);
  }
  protected saveFilter(): void {
    if (this.filterName.valid) void this.saved.save(this.filterName.value, this.form.getRawValue());
  }
  protected useFilter(filters: ReturnType<typeof this.form.getRawValue>): void {
    this.form.setValue(filters);
    const days = Temporal.PlainDate.from(filters.from).until(
      Temporal.PlainDate.from(filters.until),
    ).days;
    this.view.set(days <= 1 ? 'timeGridDay' : days <= 7 ? 'timeGridWeek' : 'dayGridMonth');
    this.dateInput.setValue(filters.from);
    this.apply();
  }
}
