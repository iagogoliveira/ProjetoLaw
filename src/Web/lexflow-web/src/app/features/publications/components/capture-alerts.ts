import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoDirective } from '@jsverse/transloco';
import { CaptureAlertsStore } from '../data/capture-alerts-store';
import { legalInstant, legalToday } from '../data/publications-format';
import { PublicationErrors } from './publication-errors';

@Component({
  selector: 'lf-capture-alerts',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoDirective,
    PublicationErrors,
  ],
  providers: [CaptureAlertsStore],
  template: `<section *transloco="let t" class="page">
    <h2>{{ t('publications.alerts.title') }}</h2>
    <form class="inline" (ngSubmit)="store.load(day.value)">
      <mat-form-field appearance="outline"
        ><mat-label>{{ t('publications.alerts.day') }}</mat-label
        ><input matInput type="date" [formControl]="day" required
      /></mat-form-field>
      <button mat-button type="submit" [disabled]="!day.value || store.loading()">
        {{ t('publications.alerts.inspect') }}
      </button>
    </form>
    <p class="hint">{{ t('publications.alerts.hint') }}</p>
    <lf-publication-errors [code]="store.errorCode()" />
    <ul>
      @for (a of store.items(); track a.id) {
        <li>
          {{ instant(a.raisedAt) }} —
          {{
            t(
              a.resolvedAt ? 'publications.sources.alertResolved' : 'publications.sources.alertOpen'
            )
          }}<lf-publication-errors [code]="a.resolvedAt ? null : a.errorCode" />
        </li>
      } @empty {
        <li>{{ t('publications.alerts.empty') }}</li>
      }
    </ul>
  </section>`,
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CaptureAlerts implements OnInit {
  protected readonly store = inject(CaptureAlertsStore);
  protected readonly day = new FormControl(legalToday(), { nonNullable: true });
  protected readonly instant = legalInstant;
  ngOnInit(): void {
    void this.store.load(this.day.value);
  }
}
