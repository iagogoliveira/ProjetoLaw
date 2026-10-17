import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { TranslocoDirective } from '@jsverse/transloco';
import type { SessionResponse } from '../../../api/types.gen';

/** Sessões abertas; a atual é marcada e não pode ser encerrada daqui (para isso existe "Sair"). */
@Component({
  selector: 'lf-sessions-list',
  imports: [DatePipe, MatButtonModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      @if (sessions().length === 0) {
        <p class="empty">{{ t('account.sessions.empty') }}</p>
      } @else {
        <ul class="list">
          @for (session of sessions(); track session.id) {
            <li class="item">
              <div>
                <p class="device">
                  {{ session.userAgent || t('account.sessions.unknownDevice') }}
                  @if (session.current) {
                    <strong class="current">{{ t('account.sessions.current') }}</strong>
                  }
                </p>
                <p class="details">
                  {{
                    t('account.sessions.lastSeen', { date: (session.lastSeenAt | date: 'short') })
                  }}
                  @if (session.ipAddress) {
                    · {{ session.ipAddress }}
                  }
                </p>
              </div>
              @if (!session.current) {
                <button
                  mat-stroked-button
                  type="button"
                  [disabled]="busy()"
                  (click)="endSession.emit(session.id)"
                >
                  {{ t('account.sessions.end') }}
                </button>
              }
            </li>
          }
        </ul>
      }
    </ng-container>
  `,
  styles: `
    .list {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: var(--lf-space-md);
      padding: var(--lf-space-sm) 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .device,
    .details {
      margin: 0;
    }

    .details,
    .empty {
      color: var(--mat-sys-on-surface-variant);
    }

    .current {
      margin-inline-start: var(--lf-space-sm);
      color: var(--mat-sys-primary);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionsList {
  readonly sessions = input.required<SessionResponse[]>();
  readonly busy = input(false);
  readonly endSession = output<string>();
}
