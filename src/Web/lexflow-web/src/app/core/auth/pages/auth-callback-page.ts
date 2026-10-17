import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { SIGN_IN_PATH } from '../auth-config';
import { INVITATION_PATH, PendingInvitation } from '../../organization/pending-invitation';
import { AuthSession } from '../auth-session';

/**
 * Retorno do servidor de identidade: o código já foi trocado por tokens no bootstrap (`AuthSession.initialize`).
 * Se a pessoa saiu de um convite para entrar ou criar a conta, volta para ele.
 */
@Component({
  selector: 'lf-auth-callback-page',
  imports: [TranslocoDirective],
  template: `
    <main id="main-content" *transloco="let t" class="page" tabindex="-1">
      <p role="status">{{ t('auth.callback.signingIn') }}</p>
    </main>
  `,
  styles: `
    .page {
      min-height: 100vh;
      display: grid;
      place-items: center;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCallbackPage implements OnInit {
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);

  async ngOnInit(): Promise<void> {
    await this.session.initialize();
    const invitation = PendingInvitation.peek();
    const target = !this.session.isAuthenticated()
      ? SIGN_IN_PATH
      : invitation
        ? `${INVITATION_PATH}?token=${encodeURIComponent(invitation)}`
        : '/';
    await this.router.navigateByUrl(target, { replaceUrl: true });
  }
}
