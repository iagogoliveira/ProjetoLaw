import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoDirective } from '@jsverse/transloco';
import qrcode from 'qrcode-generator';
import type { TotpSetupResponse } from '../../../api/types.gen';

/** QR code (GIF em data URL, aceito pelo sanitizador do Angular) a partir do link `otpauth://`. */
export function qrDataUrl(text: string): string {
  const qr = qrcode(0, 'M');
  qr.addData(text);
  qr.make();
  return qr.createDataURL(4, 4);
}

/**
 * Verificação em duas etapas: escolha entre aplicativo autenticador (QR code ou chave, depois confirmação com um
 * código) e código por e-mail. Desligar o MFA ainda não é possível (ADR 0015).
 */
@Component({
  selector: 'lf-mfa-setup',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
    TranslocoDirective,
  ],
  template: `
    <ng-container *transloco="let t">
      @if (method() !== 'none') {
        <p class="status" role="status">{{ t('account.mfa.enabled.' + method()) }}</p>
        @if (recoveryCodes().length > 0) {
          <section class="recovery" aria-labelledby="recovery-title">
            <h3 id="recovery-title">{{ t('account.mfa.recovery.title') }}</h3>
            <p>{{ t('account.mfa.recovery.hint') }}</p>
            <ul class="codes">
              @for (code of recoveryCodes(); track code) {
                <li>
                  <code>{{ code }}</code>
                </li>
              }
            </ul>
          </section>
        }
      } @else if (setup(); as current) {
        <p>{{ t('account.mfa.totp.scan') }}</p>
        <img class="qr" [src]="qr()" [alt]="t('account.mfa.totp.qrAlt')" width="200" height="200" />
        <p>
          {{ t('account.mfa.totp.key') }}
          <code class="key" data-testid="totp-key">{{ current.sharedKey }}</code>
        </p>
        <form class="confirm" [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field appearance="outline">
            <mat-label>{{ t('account.mfa.totp.code') }}</mat-label>
            <input
              matInput
              formControlName="code"
              autocomplete="one-time-code"
              inputmode="numeric"
              maxlength="16"
            />
          </mat-form-field>
          <div class="actions">
            <button mat-flat-button type="submit" [disabled]="busy() || form.invalid">
              {{ t('account.mfa.totp.confirm') }}
            </button>
            <button mat-button type="button" (click)="cancelTotp.emit()">
              {{ t('account.mfa.totp.cancel') }}
            </button>
          </div>
        </form>
      } @else {
        <p>{{ t('account.mfa.intro') }}</p>
        <div class="actions">
          <button mat-flat-button type="button" [disabled]="busy()" (click)="startTotp.emit()">
            {{ t('account.mfa.totp.start') }}
          </button>
          <button mat-stroked-button type="button" [disabled]="busy()" (click)="enableEmail.emit()">
            {{ t('account.mfa.email.enable') }}
          </button>
        </div>
      }
    </ng-container>
  `,
  styles: `
    .qr {
      display: block;
      margin: var(--lf-space-sm) 0;
      image-rendering: pixelated;
    }

    .key {
      font-size: 1.1em;
      letter-spacing: 1px;
      word-break: break-all;
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--lf-space-sm);
    }

    .codes {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
      gap: var(--lf-space-xs, 4px);
      padding: 0;
      list-style: none;
    }

    .status {
      color: var(--mat-sys-primary);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MfaSetup {
  readonly method = input.required<string>();
  readonly setup = input<TotpSetupResponse | null>(null);
  readonly recoveryCodes = input<string[]>([]);
  readonly busy = input(false);

  readonly startTotp = output();
  readonly confirmTotp = output<string>();
  readonly cancelTotp = output();
  readonly enableEmail = output();

  protected readonly form = new FormGroup({
    code: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  protected readonly qr = computed(() => {
    const current = this.setup();
    return current ? qrDataUrl(current.authenticatorUri) : '';
  });

  protected submit(): void {
    if (this.form.valid) {
      this.confirmTotp.emit(this.form.controls.code.value);
      this.form.reset();
    }
  }
}
