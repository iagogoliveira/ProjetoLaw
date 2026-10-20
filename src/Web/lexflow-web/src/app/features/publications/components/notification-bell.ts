import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { NotificationResponse } from '../../../api/types.gen';
import { CanDirective } from '../../../core/organization/can.directive';
import { PublicationsLiveStore } from '../data/publications-live-store';
import { PublicationErrors } from './publication-errors';
import { legalInstant } from '../data/publications-format';

@Component({
  selector: 'lf-notification-bell',
  imports: [MatButtonModule, RouterLink, TranslocoDirective, CanDirective, PublicationErrors],
  template: `<ng-container *transloco="let t">
    <button
      mat-button
      type="button"
      (click)="opened.set(!opened())"
      [attr.aria-expanded]="opened()"
      aria-controls="notification-panel"
    >
      {{ t('publications.notifications.title') }}
    </button>
    @if (opened()) {
      <section
        id="notification-panel"
        [attr.aria-label]="t('publications.notifications.title')"
        class="notifications"
      >
        <h2>{{ t('publications.notifications.title') }}</h2>
        <p role="status">
          {{
            t(
              live.online()
                ? 'publications.notifications.online'
                : 'publications.notifications.reconnecting'
            )
          }}
        </p>
        <lf-publication-errors [code]="live.errorCode()" />
        <button mat-button type="button" (click)="live.refresh()">
          {{ t('publications.refresh') }}
        </button>
        <ul>
          @for (n of live.items(); track n.id) {
            <li>
              <span>{{ t(labelFor(n.kind)) }}</span>
              <time>{{ instant(n.createdAt) }}</time>
              @if (n.kind === 'publications.received') {
                <a
                  *lfCan="'publications.read'"
                  routerLink="/publicacoes"
                  [queryParams]="{ publication: n.resourceId }"
                  (click)="opened.set(false)"
                  >{{ t('publications.notifications.open') }}</a
                >
              } @else {
                @if (isScheduling(n.kind)) {
                  <a
                    *lfCan="'cases.read'"
                    [routerLink]="caseRoute(n)"
                    (click)="opened.set(false)"
                    >{{ t('publications.notifications.openCase') }}</a
                  >
                } @else {
                  <a
                    *lfCan="'settings.integrations.manage'"
                    routerLink="/publicacoes/fontes"
                    (click)="opened.set(false)"
                    >{{ t('publications.sources.title') }}</a
                  >
                }
              }
              <button
                mat-button
                type="button"
                (click)="live.read(n.id)"
                [attr.aria-label]="t('publications.notifications.readAt', { date: n.createdAt })"
              >
                {{ t('publications.notifications.read') }}
              </button>
            </li>
          } @empty {
            <li>{{ t('publications.notifications.empty') }}</li>
          }
        </ul>
        @if (live.nextCursor()) {
          <button mat-button type="button" [disabled]="live.loading()" (click)="live.more()">
            {{ t('publications.more') }}
          </button>
        }
      </section>
    }
  </ng-container>`,
  styles: `
    .notifications {
      position: absolute;
      z-index: 10;
      inset-inline-end: 1rem;
      top: 4rem;
      width: min(480px, 90vw);
      max-height: 75vh;
      overflow: auto;
      padding: 1rem;
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      border: 1px solid var(--mat-sys-outline);
      border-radius: 8px;
      font: var(--mat-sys-body-medium);
      white-space: normal;
    }
    h2 {
      font: var(--mat-sys-title-medium);
    }
    ul {
      padding: 1rem;
    }
    li {
      display: grid;
      gap: 0.5rem;
      margin-bottom: 1rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationBell {
  protected readonly live = inject(PublicationsLiveStore);
  protected readonly opened = signal(false);
  protected readonly instant = legalInstant;

  protected labelFor(kind: string): string {
    if (this.isScheduling(kind)) return kind;
    return kind === 'publications.received'
      ? 'publications.notifications.publication'
      : 'publications.notifications.capture';
  }

  protected isScheduling(kind: string): boolean {
    return kind === 'scheduling.deadline_missed' || kind === 'scheduling.calendar_changed';
  }

  protected caseRoute(notification: NotificationResponse): string[] {
    const caseId = notification.caseId;
    return caseId ? ['/processos', caseId] : ['/processos'];
  }
}
