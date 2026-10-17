import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { TranslocoDirective } from '@jsverse/transloco';
import { AuthSession } from '../auth-session';

/** Página pública: o login e o cadastro acontecem no servidor de identidade, não no SPA. */
@Component({
  selector: 'lf-sign-in-page',
  imports: [MatButtonModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      <main id="main-content" class="page" tabindex="-1">
        <section class="card">
          <p class="brand">{{ t('app.name') }}</p>
          <h1 class="title">{{ t('auth.signIn.title') }}</h1>
          <p class="lead">{{ t('auth.signIn.lead') }}</p>
          <button mat-flat-button type="button" (click)="session.signIn()">
            {{ t('auth.signIn.action') }}
          </button>
          <a mat-button [href]="session.registerUrl()">{{ t('auth.signIn.register') }}</a>
        </section>
      </main>
    </ng-container>
  `,
  styles: `
    .page {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: var(--lf-space-md);
    }

    .card {
      display: flex;
      flex-direction: column;
      gap: var(--lf-space-md);
      width: min(420px, 100%);
      padding: var(--lf-space-lg);
      border-radius: var(--mat-sys-corner-large);
      background: var(--mat-sys-surface-container);
    }

    .brand {
      margin: 0;
      font: var(--mat-sys-title-large);
    }

    .title {
      margin: 0;
      font: var(--mat-sys-headline-small);
    }

    .lead {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInPage {
  protected readonly session = inject(AuthSession);
}
