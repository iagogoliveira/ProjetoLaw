import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AuthSession } from '../../../core/auth/auth-session';
import { PendingInvitation } from '../../../core/organization/pending-invitation';
import { InvitationStore } from '../data/invitation-store';

/**
 * Página do link do convite (`/convite?token=…`), pública. Antes do login mostra o escritório e oferece "Entrar" ou
 * "Criar conta" com o e-mail convidado já preenchido; o token fica guardado na aba e a página volta depois do login.
 * Já autenticado, a pessoa aceita e entra no escritório com os perfis do convite.
 */
@Component({
  selector: 'lf-invitation-page',
  imports: [DatePipe, MatButtonModule, TranslocoDirective],
  providers: [InvitationStore],
  template: `
    <ng-container *transloco="let t">
      <main id="main-content" class="page" tabindex="-1">
        <section class="card">
          <p class="brand">{{ t('app.name') }}</p>
          @if (store.loading()) {
            <p role="status">{{ t('invitation.loading') }}</p>
          } @else if (store.preview(); as preview) {
            <h1 class="title">
              {{ t('invitation.title', { organization: preview.organizationName }) }}
            </h1>
            <p class="lead">
              {{
                t('invitation.lead', {
                  email: preview.email,
                  date: (preview.expiresAt | date: 'short'),
                })
              }}
            </p>
            @if (store.errorCode(); as code) {
              <p class="error" role="alert">{{ t('errors.' + code) }}</p>
            }
            @if (session.user(); as user) {
              <p>{{ t('invitation.signedInAs', { email: user.email }) }}</p>
              <div class="actions">
                <button mat-flat-button type="button" [disabled]="store.busy()" (click)="accept()">
                  {{ t('invitation.accept') }}
                </button>
                <button mat-button type="button" (click)="session.signOut()">
                  {{ t('invitation.switchAccount') }}
                </button>
              </div>
            } @else {
              <div class="actions">
                <button mat-flat-button type="button" (click)="session.signIn()">
                  {{ t('invitation.signIn') }}
                </button>
                <a mat-stroked-button [href]="session.registerUrlFor(preview.email)">
                  {{ t('invitation.register') }}
                </a>
              </div>
              <p class="hint">{{ t('invitation.registerHint') }}</p>
            }
          } @else if (store.errorCode(); as code) {
            <h1 class="title">{{ t('invitation.invalidTitle') }}</h1>
            <p class="error" role="alert">{{ t('errors.' + code) }}</p>
          }
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
      width: min(480px, 100%);
      padding: var(--lf-space-lg);
      border-radius: var(--mat-sys-corner-large);
      background: var(--mat-sys-surface-container);
    }

    .brand,
    .title,
    .lead,
    .hint {
      margin: 0;
    }

    .brand {
      font: var(--mat-sys-title-large);
    }

    .title {
      font: var(--mat-sys-headline-small);
    }

    .lead,
    .hint {
      color: var(--mat-sys-on-surface-variant);
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--lf-space-sm);
    }

    .error {
      color: var(--mat-sys-error);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvitationPage implements OnInit {
  protected readonly store = inject(InvitationStore);
  protected readonly session = inject(AuthSession);
  private readonly router = inject(Router);

  /** Query string do link (`withComponentInputBinding`). */
  readonly token = input<string | undefined>();

  async ngOnInit(): Promise<void> {
    await this.session.initialize();
    await this.store.open(this.token() ?? PendingInvitation.peek());
  }

  async accept(): Promise<void> {
    if (await this.store.accept()) {
      await this.router.navigateByUrl('/');
    }
  }
}
