import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import { debounceTime } from 'rxjs';
import type {
  CaseOptionsResponse,
  HearingFormat,
  HearingResponse,
  HearingType,
} from '../../../../api/types.gen';
import { CanDirective } from '../../../../core/organization/can.directive';
import { HearingsStore } from '../../data/hearings-store';
import { hearingInstant, localDateTime } from '../../data/values-format';
import { ValueErrors } from './value-errors';

@Component({
  selector: 'lf-hearings-tab',
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
  providers: [HearingsStore],
  templateUrl: './hearings-tab.html',
  styleUrl: '../../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HearingsTab implements OnInit {
  readonly caseId = input.required<string>();
  readonly options = input<CaseOptionsResponse | null>(null);
  readonly closed = input(false);
  readonly changed = output<void>();
  protected readonly store = inject(HearingsStore);
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly destroyRef = inject(DestroyRef);
  protected readonly editing = signal<string | null | undefined>(undefined);
  protected readonly pending = signal<{
    id: string;
    action: 'reschedule' | 'hold' | 'cancel';
  } | null>(null);
  protected readonly types: HearingType[] = [
    'conciliation',
    'instruction',
    'combined',
    'judgment',
    'other',
  ];
  protected readonly formats: HearingFormat[] = ['inPerson', 'virtual', 'hybrid'];
  protected readonly search = this.fb.control('');
  protected readonly form = this.fb.group({
    type: 'conciliation' as HearingType,
    format: 'inPerson' as HearingFormat,
    scheduledAt: ['', Validators.required],
    location: '',
    meetingLink: ['', Validators.pattern(/^https?:\/\/\S+$/)],
    lawyerUserId: null as string | null,
    representative: '',
    witnessPersonIds: this.fb.control<string[]>([]),
    notes: '',
  });
  protected readonly actionForm = this.fb.group({ newDate: '', text: '' });
  protected readonly dateTime = (value: string) =>
    new Intl.DateTimeFormat('pt-BR', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: 'America/Sao_Paulo',
    }).format(new Date(value));
  ngOnInit(): void {
    void this.store.load(this.caseId());
    this.search.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe((term) => void this.store.searchPeople(term));
  }
  protected memberName(id: string | null): string {
    return this.options()?.members.find((m) => m.userId === id)?.name ?? '—';
  }
  protected edit(h: HearingResponse | null): void {
    this.form.reset();
    this.form.controls.scheduledAt.enable();
    this.editing.set(h?.id ?? null);
    if (h) {
      this.form.patchValue({
        ...h,
        scheduledAt: localDateTime(h.scheduledAt),
        location: h.location ?? '',
        meetingLink: h.meetingLink ?? '',
        representative: h.representative ?? '',
        notes: h.notes ?? '',
      });
      this.form.controls.scheduledAt.disable();
    }
  }
  protected witness(id: string, add: boolean): void {
    const control = this.form.controls.witnessPersonIds;
    control.setValue(
      add ? [...new Set([...control.value, id])] : control.value.filter((v) => v !== id),
    );
  }
  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    const v = this.form.getRawValue();
    if (this.form.invalid || !v.scheduledAt || this.editing() === undefined) return;
    if (
      await this.store.save(this.editing() ?? null, {
        ...v,
        scheduledAt: hearingInstant(v.scheduledAt),
        location: v.format === 'virtual' ? null : v.location.trim() || null,
        meetingLink: v.format === 'inPerson' ? null : v.meetingLink.trim() || null,
        representative: v.representative.trim() || null,
        notes: v.notes.trim() || null,
      })
    ) {
      this.editing.set(undefined);
      this.changed.emit();
    }
  }
  protected open(h: HearingResponse, action: 'reschedule' | 'hold' | 'cancel'): void {
    this.pending.set({ id: h.id, action });
    this.actionForm.reset({ newDate: localDateTime(h.scheduledAt), text: '' });
  }
  protected async act(): Promise<void> {
    const p = this.pending()!;
    const v = this.actionForm.getRawValue();
    if (p.action === 'reschedule' && !v.newDate) return;
    const done =
      p.action === 'reschedule'
        ? await this.store.reschedule(p.id, {
            newDate: hearingInstant(v.newDate),
            reason: v.text.trim() || null,
          })
        : await this.store.end(p.id, p.action, v.text.trim());
    if (done) {
      this.pending.set(null);
      this.changed.emit();
    }
  }
}
