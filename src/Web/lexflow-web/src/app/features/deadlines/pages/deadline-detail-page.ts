import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { DeadlineDetailStore } from '../data/deadline-detail-store';
import { errorKey, formatDate } from '../data/deadline-format';

@Component({
  selector: 'lf-deadline-detail-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslocoDirective,
    CanDirective,
  ],
  templateUrl: './deadline-detail-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DeadlineDetailStore],
})
export class DeadlineDetailPage {
  private readonly route = inject(ActivatedRoute);
  protected readonly store = inject(DeadlineDetailStore);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    evidence: ['', [Validators.required, Validators.maxLength(4000)]],
    reason: ['', [Validators.required, Validators.maxLength(4000)]],
  });
  protected readonly date = formatDate;
  protected readonly errorTranslationKey = errorKey;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      this.form.reset({ evidence: '', reason: '' });
      const id = params.get('id');
      if (id) void this.store.load(id);
    });
  }

  protected async load(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) await this.store.load(id);
  }

  protected act(action: 'start' | 'review' | 'complete' | 'miss'): void {
    if (this.store.busy()) return;
    const evidence = this.form.controls.evidence;
    const reason = this.form.controls.reason;
    if (action === 'complete' && (evidence.invalid || !evidence.value.trim())) {
      evidence.markAsTouched();
      return;
    }
    if (action === 'miss' && (reason.invalid || !reason.value.trim())) {
      reason.markAsTouched();
      return;
    }
    void this.store.act(action, evidence.value, reason.value);
  }
}
