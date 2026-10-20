import { ChangeDetectionStrategy, Component, inject, input, OnInit, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { hearingInstant } from '../../cases/data/values-format';
import { MovementsStore } from '../data/movements-store';
import { legalInstant } from '../data/publications-format';
import { PublicationErrors } from './publication-errors';

@Component({
  selector: 'lf-movements-tab',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoDirective,
    CanDirective,
    PublicationErrors,
  ],
  providers: [MovementsStore],
  template: `<section *transloco="let t" class="page">
    <h2>{{ t('publications.movements.title') }}</h2>
    <lf-publication-errors [code]="store.errorCode()" />
    <button
      mat-button
      type="button"
      [disabled]="store.loading() || store.busy()"
      (click)="store.load(caseId())"
    >
      {{ t('publications.refresh') }}
    </button>
    @if (store.sync(); as sync) {
      <p>{{ t('publications.movements.lastSync', { at: instant(sync.lastSuccessAt) }) }}</p>
      <lf-publication-errors [code]="sync.errorCode" />
    }
    <ol>
      @for (m of store.items(); track m.id) {
        <li>
          <strong>{{ instant(m.occurredAt) }}</strong> —
          {{ t('publications.movements.origins.' + m.origin) }}
          <p>{{ m.description }}</p>
          <small>{{ t('publications.movements.collected', { at: instant(m.collectedAt) }) }}</small>
        </li>
      } @empty {
        <li>{{ t('publications.movements.empty') }}</li>
      }
    </ol>
    @if (store.nextCursor()) {
      <button mat-button type="button" [disabled]="store.loading()" (click)="store.more()">
        {{ t('publications.more') }}
      </button>
    }
    <form *lfCan="'cases.edit'" [formGroup]="form" (ngSubmit)="save()">
      <h3>{{ t('publications.movements.new') }}</h3>
      <mat-form-field appearance="outline"
        ><mat-label>{{ t('publications.movements.occurredAt') }}</mat-label
        ><input matInput type="datetime-local" formControlName="occurredAt" required /><mat-hint>{{
          t('cases.hearings.timeZone')
        }}</mat-hint></mat-form-field
      >
      <mat-form-field appearance="outline"
        ><mat-label>{{ t('publications.movements.description') }}</mat-label
        ><textarea matInput formControlName="description" required maxlength="20000"></textarea>
      </mat-form-field>
      <button mat-flat-button type="submit" [disabled]="form.invalid || store.busy()">
        {{ t('publications.movements.save') }}
      </button>
    </form>
  </section>`,
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MovementsTab implements OnInit {
  readonly caseId = input.required<string>();
  readonly changed = output<void>();
  protected readonly store = inject(MovementsStore);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly form = this.fb.group({
    occurredAt: ['', Validators.required],
    description: ['', [Validators.required, Validators.maxLength(20000)]],
  });
  protected readonly instant = legalInstant;
  ngOnInit(): void {
    void this.store.load(this.caseId());
  }
  protected async save(): Promise<void> {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    if (
      await this.store.save({
        occurredAt: hearingInstant(v.occurredAt),
        description: v.description,
      })
    ) {
      this.form.reset();
      this.changed.emit();
    }
  }
}
