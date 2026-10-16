import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

/** Página inicial vazia; os indicadores entram com os módulos de negócio. */
@Component({
  selector: 'lf-dashboard-page',
  imports: [TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      <h1 class="title">{{ t('dashboard.title') }}</h1>
      <p class="empty">{{ t('dashboard.empty') }}</p>
    </ng-container>
  `,
  styles: `
    .title {
      margin: 0 0 var(--lf-space-md);
      font: var(--mat-sys-headline-small);
    }

    .empty {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {}
