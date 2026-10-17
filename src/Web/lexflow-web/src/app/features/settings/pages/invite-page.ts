import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import type { DataScope } from '../../../api/types.gen';
import { InviteStore } from '../data/invite-store';

/**
 * Convidar uma pessoa por e-mail (RF-01): o convite vale 7 dias e a pessoa entra com o perfil escolhido. A tela
 * de usuários e convites (`users-page`) lista, reenvia e cancela.
 */
@Component({
  selector: 'lf-invite-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
    TranslocoDirective,
  ],
  providers: [InviteStore],
  template: `
    <section class="page" *transloco="let t">
      <h2>{{ t('settings.invite.title') }}</h2>
      <p class="lead">{{ t('settings.invite.lead') }}</p>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline">
          <mat-label>{{ t('settings.invite.email') }}</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="off" />
          @if (form.controls.email.invalid) {
            <mat-error>{{ t('settings.invite.emailInvalid') }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ t('settings.invite.role') }}</mat-label>
          <mat-select formControlName="roleId">
            @for (role of store.roles(); track role.id) {
              <mat-option [value]="role.id">{{ role.name }}</mat-option>
            }
          </mat-select>
          @if (form.controls.roleId.invalid) {
            <mat-error>{{ t('settings.invite.roleRequired') }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ t('settings.invite.scope') }}</mat-label>
          <mat-select formControlName="scope">
            <mat-option value="own">{{ t('settings.scope.own') }}</mat-option>
            <mat-option value="organization">{{ t('settings.scope.organization') }}</mat-option>
          </mat-select>
        </mat-form-field>
        @if (store.errorCode(); as code) {
          <p class="error" role="alert">{{ t('errors.' + code) }}</p>
        }
        @if (store.sent(); as sent) {
          <p class="status" role="status">{{ t('settings.invite.sent', { email: sent.email }) }}</p>
        }
        <button mat-flat-button type="submit" [disabled]="store.busy()">
          {{ t('settings.invite.action') }}
        </button>
      </form>
    </section>
  `,
  styles: `
    .page {
      display: grid;
      gap: var(--lf-space-md);
      max-width: 520px;
    }

    form {
      display: grid;
      gap: var(--lf-space-sm);
      justify-items: start;
    }

    mat-form-field {
      width: 100%;
    }

    .lead {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }

    .error {
      color: var(--mat-sys-error);
    }

    .status {
      color: var(--mat-sys-primary);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvitePage implements OnInit {
  protected readonly store = inject(InviteStore);

  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    // Desabilitado até os perfis chegarem: ninguém abre uma lista vazia.
    roleId: new FormControl(
      { value: '', disabled: true },
      { nonNullable: true, validators: [Validators.required] },
    ),
    scope: new FormControl<DataScope>('own', { nonNullable: true }),
  });

  async ngOnInit(): Promise<void> {
    await this.store.loadRoles();
    if (this.store.roles().length > 0) {
      this.form.controls.roleId.enable();
    }
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, roleId, scope } = this.form.getRawValue();
    if (await this.store.invite(email.trim(), roleId, scope)) {
      this.form.reset({ email: '', roleId, scope });
    }
  }
}
