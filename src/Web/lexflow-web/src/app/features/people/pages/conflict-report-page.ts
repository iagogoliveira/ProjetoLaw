import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { ConflictRisk } from '../../../api/types.gen';
import { ConflictStore } from '../data/conflict-store';
import { formatDocument } from '../data/people-format';

const RISKS: readonly ConflictRisk[] = ['direct', 'indirect', 'none'];

/** Relatório das verificações de conflito (RF-07), para auditoria e compliance da OAB, com filtro por risco. */
@Component({
  selector: 'lf-conflict-report-page',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [ConflictStore],
  template: `
    <section class="page" *transloco="let t">
      <a mat-button routerLink="..">{{ t('conflicts.backToCheck') }}</a>
      <h1>{{ t('conflicts.reportTitle') }}</h1>

      <mat-form-field appearance="outline" class="filter">
        <mat-label>{{ t('conflicts.risk') }}</mat-label>
        <mat-select [ngModel]="store.risk()" (ngModelChange)="store.loadReport($event)">
          <mat-option [value]="null">{{ t('conflicts.allRisks') }}</mat-option>
          @for (risk of risks; track risk) {
            <mat-option [value]="risk">{{ t('conflicts.risks.' + risk) }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      @if (store.errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }

      @if (store.report().length === 0 && !store.busy()) {
        <p class="empty">{{ t('conflicts.emptyReport') }}</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th scope="col">{{ t('conflicts.date') }}</th>
              <th scope="col">{{ t('conflicts.name') }}</th>
              <th scope="col">{{ t('conflicts.side') }}</th>
              <th scope="col">{{ t('conflicts.risk') }}</th>
              <th scope="col">{{ t('conflicts.decision') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (check of store.report(); track check.id) {
              <tr>
                <td>{{ check.createdAt | date: 'short' }}</td>
                <td>
                  {{ check.queryName }}
                  @if (check.queryDocument) {
                    <span class="hint"> · {{ format(check.queryDocument) }}</span>
                  }
                </td>
                <td>{{ t('conflicts.sides.' + check.side) }}</td>
                <td [class]="'risk-' + check.risk">{{ t('conflicts.risks.' + check.risk) }}</td>
                <td>
                  @if (check.proceeded) {
                    {{ t('conflicts.proceeded') }}
                    @if (check.justification) {
                      <span class="hint"> — {{ check.justification }}</span>
                    }
                  } @else {
                    {{ t('conflicts.pending') }}
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (store.nextCursor()) {
        <button mat-button type="button" [disabled]="store.busy()" (click)="store.moreReport()">
          {{ t('people.loadMore') }}
        </button>
      }
    </section>
  `,
  styleUrl: '../../../shared/ui/page.scss',
  styles: `
    .filter {
      max-width: 280px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConflictReportPage implements OnInit {
  protected readonly store = inject(ConflictStore);
  protected readonly risks = RISKS;
  protected readonly format = formatDocument;

  ngOnInit(): Promise<void> {
    return this.store.loadReport(null);
  }
}
