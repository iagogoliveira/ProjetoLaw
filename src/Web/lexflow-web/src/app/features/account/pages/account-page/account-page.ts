import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { MfaSetup } from '../../components/mfa-setup';
import { SessionsList } from '../../components/sessions-list';
import { AccountStore } from '../../data/account-store';

/** "Minha conta": perfil, verificação em duas etapas e sessões abertas. */
@Component({
  selector: 'lf-account-page',
  imports: [DatePipe, MfaSetup, SessionsList, TranslocoDirective],
  providers: [AccountStore],
  template: `
    <ng-container *transloco="let t">
      <h1 class="title">{{ t('account.title') }}</h1>

      @if (store.errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }

      @if (store.profile(); as profile) {
        <section class="card" aria-labelledby="profile-title">
          <h2 id="profile-title">{{ t('account.profile.title') }}</h2>
          <dl class="profile">
            <dt>{{ t('account.profile.name') }}</dt>
            <dd>{{ profile.fullName }}</dd>
            <dt>{{ t('account.profile.email') }}</dt>
            <dd>{{ profile.email }}</dd>
            <dt>{{ t('account.profile.since') }}</dt>
            <dd>{{ profile.createdAt | date: 'longDate' }}</dd>
          </dl>
        </section>

        <section class="card" aria-labelledby="mfa-title">
          <h2 id="mfa-title">{{ t('account.mfa.title') }}</h2>
          <lf-mfa-setup
            [method]="store.mfaMethod()"
            [setup]="store.totpSetup()"
            [recoveryCodes]="store.recoveryCodes()"
            [busy]="store.busy()"
            (startTotp)="store.startTotp()"
            (confirmTotp)="store.confirmTotp($event)"
            (cancelTotp)="store.cancelTotp()"
            (enableEmail)="store.enableEmailMfa()"
          />
        </section>

        <section class="card" aria-labelledby="sessions-title">
          <h2 id="sessions-title">{{ t('account.sessions.title') }}</h2>
          <lf-sessions-list
            [sessions]="store.sessions()"
            [busy]="store.busy()"
            (endSession)="store.endSession($event)"
          />
        </section>
      } @else if (store.loading()) {
        <p role="status">{{ t('account.loading') }}</p>
      }
    </ng-container>
  `,
  styles: `
    .title {
      margin: 0 0 var(--lf-space-md);
      font: var(--mat-sys-headline-small);
    }

    .card {
      max-width: 720px;
      margin-bottom: var(--lf-space-lg);
      padding: var(--lf-space-md) var(--lf-space-lg);
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-surface-container-low);
    }

    h2 {
      margin-top: 0;
      font: var(--mat-sys-title-medium);
    }

    .profile {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: var(--lf-space-xs, 4px) var(--lf-space-md);
      margin: 0;
    }

    .profile dt {
      color: var(--mat-sys-on-surface-variant);
    }

    .profile dd {
      margin: 0;
    }

    .error {
      color: var(--mat-sys-error);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountPage implements OnInit {
  protected readonly store = inject(AccountStore);

  ngOnInit(): void {
    void this.store.load();
  }
}
