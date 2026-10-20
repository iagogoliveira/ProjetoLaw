import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { PublicationsLiveStore } from '../data/publications-live-store';

@Component({
  selector: 'lf-pending-badge',
  imports: [TranslocoDirective],
  template: `<ng-container *transloco="let t">
    @if (live.pendingCount(); as count) {
      <span [attr.aria-label]="t('publications.pendingCount', { count })">({{ count }})</span>
    }
  </ng-container>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PendingBadge {
  protected readonly live = inject(PublicationsLiveStore);
}
