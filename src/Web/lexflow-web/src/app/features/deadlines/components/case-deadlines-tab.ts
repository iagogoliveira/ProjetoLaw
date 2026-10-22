import { ChangeDetectionStrategy, Component, inject, input, OnChanges } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { CaseDeadlinesStore } from '../data/case-deadlines-store';
import { errorKey, formatDate } from '../data/deadline-format';

@Component({
  selector: 'lf-case-deadlines-tab',
  providers: [CaseDeadlinesStore],
  imports: [MatButtonModule, RouterLink, TranslocoDirective, CanDirective],
  template: `
    <section class="panel" *transloco="let t">
      <div class="actions">
        <a mat-button routerLink="/prazos" [queryParams]="{ caseId: caseId() }">{{
          t('deadlines.title')
        }}</a>
        <a
          *lfCan="'schedule.deadlines.manage'"
          mat-flat-button
          routerLink="/prazos/novo"
          [queryParams]="{ caseId: caseId() }"
          >{{ t('deadlines.create.title') }}</a
        >
        <button mat-button type="button" [disabled]="loading()" (click)="load()">
          {{ t('deadlines.refresh') }}
        </button>
      </div>
      @if (error(); as code) {
        <p role="alert" class="error">{{ t(errorKey(code)) }}</p>
      }
      @if (loading()) {
        <p role="status">{{ t('deadlines.loading') }}</p>
      } @else {
        <div class="table-wrap">
          <table>
            <caption>
              {{
                t('deadlines.caseList')
              }}
            </caption>
            <thead>
              <tr>
                <th scope="col">{{ t('deadlines.fields.title') }}</th>
                <th scope="col">{{ t('deadlines.fields.internalDueOn') }}</th>
                <th scope="col">{{ t('deadlines.fields.fatalDueOn') }}</th>
                <th scope="col">{{ t('deadlines.fields.status') }}</th>
              </tr>
            </thead>
            <tbody>
              @for (d of items(); track d.id) {
                <tr>
                  <td>
                    <a [routerLink]="['/prazos', d.id]">{{ d.title }}</a>
                  </td>
                  <td>{{ formatDate(d.internalDueOn) }}</td>
                  <td>{{ formatDate(d.fatalDueOn) }}</td>
                  <td>{{ t('deadlines.status.' + d.lifecycleStatus) }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="4">{{ t('deadlines.empty') }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CaseDeadlinesTab implements OnChanges {
  readonly caseId = input.required<string>();
  private readonly store = inject(CaseDeadlinesStore);
  protected readonly items = this.store.items;
  protected readonly loading = this.store.loading;
  protected readonly error = this.store.errorCode;
  protected readonly formatDate = formatDate;
  protected readonly errorKey = errorKey;

  ngOnChanges(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    await this.store.load(this.caseId());
  }
}
