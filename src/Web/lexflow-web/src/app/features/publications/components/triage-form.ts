import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnChanges,
  SimpleChanges,
  output,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelect, MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import type {
  PublicationHearingFormat,
  PublicationHearingType,
  PublicationResponse,
  PublicationScreenOptionsResponse,
  PublicationTriageInput,
  DeadlineCalculationResponse,
  TriageAction,
  TriageClassification,
} from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { CanDirective } from '../../../core/organization/can.directive';
import { hearingInstant } from '../../cases/data/values-format';
import { DeadlineCalculator } from '../../deadlines/components/deadline-calculator';
import { PublicationsStore } from '../data/publications-store';

@Component({
  selector: 'lf-triage-form',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslocoDirective,
    CanDirective,
    DeadlineCalculator,
  ],
  templateUrl: './triage-form.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TriageForm implements OnChanges {
  readonly publication = input.required<PublicationResponse>();
  readonly options = input<PublicationScreenOptionsResponse | null>(null);
  readonly triageRequested = output<PublicationTriageInput>();
  readonly linkRequested = output<string>();
  readonly assignmentRequested = output<string | null>();
  readonly caseSearchRequested = output<string>();
  protected readonly store = inject(PublicationsStore);
  protected readonly access = inject(AccessStore);
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly classification = viewChild<MatSelect>('classification');
  protected readonly classifications: TriageClassification[] = [
    'deadlineNotice',
    'routine',
    'hearing',
    'decision',
    'judgment',
    'other',
  ];
  protected readonly types: PublicationHearingType[] = [
    'conciliation',
    'instruction',
    'combined',
    'judgment',
    'other',
  ];
  protected readonly formats: PublicationHearingFormat[] = ['inPerson', 'virtual', 'hybrid'];
  protected readonly form = this.fb.group({
    caseId: '',
    classification: 'routine' as TriageClassification,
    action: 'noAction' as TriageAction,
    reason: '',
    confirmed: [false, Validators.requiredTrue],
    title: '',
    assigneeUserId: '',
    startOn: '',
    internalDueOn: '',
    fatalDueOn: '',
    dueOn: '',
    scheduledAt: '',
    type: 'conciliation' as PublicationHearingType,
    format: 'inPerson' as PublicationHearingFormat,
    location: '',
    meetingLink: '',
    notes: '',
  });
  protected readonly assignment = this.fb.control('');
  protected readonly caseSearch = this.fb.control('');
  protected readonly calculationTriggerDate = (): string =>
    this.publication().availableOn || this.publication().publishedOn;
  private appliedCalculationDates: Pick<
    DeadlineCalculationResponse,
    'startsOn' | 'internalDueOn' | 'fatalDueOn'
  > | null = null;

  protected applyCalculation(value: { result: DeadlineCalculationResponse } | null): void {
    if (!value) {
      if (this.appliedCalculationDates) {
        const current = this.form.getRawValue();
        this.form.patchValue({
          startOn: current.startOn === this.appliedCalculationDates.startsOn ? '' : current.startOn,
          internalDueOn:
            current.internalDueOn === this.appliedCalculationDates.internalDueOn
              ? ''
              : current.internalDueOn,
          fatalDueOn:
            current.fatalDueOn === this.appliedCalculationDates.fatalDueOn
              ? ''
              : current.fatalDueOn,
          confirmed: false,
        });
        this.appliedCalculationDates = null;
      } else {
        this.form.controls.confirmed.setValue(false);
      }
      return;
    }
    this.appliedCalculationDates = {
      startsOn: value.result.startsOn,
      internalDueOn: value.result.internalDueOn,
      fatalDueOn: value.result.fatalDueOn,
    };
    this.form.patchValue({
      startOn: value.result.startsOn,
      internalDueOn: value.result.internalDueOn,
      fatalDueOn: value.result.fatalDueOn,
      confirmed: false,
    });
  }
  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['publication']) return;
    const previous = changes['publication'].previousValue as PublicationResponse | undefined;
    const current = this.publication();
    if (!previous || previous.id !== current.id) {
      this.appliedCalculationDates = null;
      this.form.reset({ caseId: current.legalCaseId ?? '' });
    } else if (previous.legalCaseId !== current.legalCaseId) {
      this.form.controls.caseId.setValue(current.legalCaseId ?? '');
    }
    if (!previous || previous.assigneeUserId !== current.assigneeUserId)
      this.assignment.setValue(current.assigneeUserId ?? '');
  }
  focus(deadline = false): void {
    if (!this.access.can('publications.triage') || this.publication().status !== 'pending') return;
    if (deadline && this.access.can('schedule.deadlines.manage')) {
      this.form.patchValue({ classification: 'deadlineNotice', action: 'deadline' });
    }
    this.classification()?.focus();
  }
  protected link(): void {
    const id = this.form.controls.caseId.value;
    if (id) this.linkRequested.emit(id);
  }
  protected assign(): void {
    this.assignmentRequested.emit(this.assignment.value || null);
  }
  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const input: PublicationTriageInput = {
      classification: v.classification,
      action: v.action,
      confirmed: v.confirmed,
      reason: v.reason || null,
      caseId: v.caseId || this.publication().legalCaseId,
    };
    if (v.action === 'deadline')
      input.deadline = {
        title: v.title,
        startOn: v.startOn,
        internalDueOn: v.internalDueOn,
        fatalDueOn: v.fatalDueOn,
        assigneeUserId: v.assigneeUserId,
      };
    if (v.action === 'task')
      input.task = { title: v.title, dueOn: v.dueOn, assigneeUserId: v.assigneeUserId };
    if (v.action === 'hearing')
      input.hearing = {
        type: v.type,
        format: v.format,
        scheduledAt: hearingInstant(v.scheduledAt),
        lawyerUserId: v.assigneeUserId,
        location: v.location || null,
        meetingLink: v.meetingLink || null,
        notes: v.notes || null,
      };
    this.triageRequested.emit(input);
  }
}
