import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

/** Página inicial (vazia na Etapa 0; indicadores chegam nas etapas de negócio). */
@Component({
  selector: 'lf-dashboard-page',
  imports: [TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      <h1>{{ t('dashboard.title') }}</h1>
      <p data-testid="empty-state">{{ t('dashboard.empty') }}</p>
    </ng-container>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {}
