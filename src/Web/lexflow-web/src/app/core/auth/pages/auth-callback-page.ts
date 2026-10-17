import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { SIGN_IN_PATH } from '../auth-config';
import { AuthSession } from '../auth-session';

/** Retorno do servidor de identidade: o código já foi trocado por tokens no bootstrap (`AuthSession.initialize`). */
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
    await this.router.navigateByUrl(this.session.isAuthenticated() ? '/' : SIGN_IN_PATH, {
      replaceUrl: true,
    });
  }
}
