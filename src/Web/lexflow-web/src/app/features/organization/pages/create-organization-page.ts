import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AccessStore } from '../../../core/organization/access-store';

/** Onboarding (Etapa 3): quem ainda não tem escritório cria o seu e vira Administrador do escritório. */
@Component({
  selector: 'lf-create-organization-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
    TranslocoDirective,
  ],
  template: `
    <section class="page" *transloco="let t">
      <h1>{{ t('organization.create.title') }}</h1>
      <p class="lead">{{ t('organization.create.lead') }}</p>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline">
          <mat-label>{{ t('organization.create.name') }}</mat-label>
          <input matInput formControlName="name" maxlength="200" autocomplete="organization" />
          @if (form.controls.name.hasError('required')) {
            <mat-error>{{ t('organization.create.nameRequired') }}</mat-error>
          }
        </mat-form-field>
        @if (access.errorCode(); as code) {
          <p class="error" role="alert">{{ t('errors.' + code) }}</p>
        }
        <button mat-flat-button type="submit" [disabled]="busy()">
          {{ t('organization.create.action') }}
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
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateOrganizationPage {
  protected readonly access = inject(AccessStore);
  private readonly router = inject(Router);
  protected readonly busy = signal(false);

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
  });

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    try {
      if (await this.access.createOrganization(this.form.controls.name.value.trim())) {
        await this.router.navigateByUrl('/');
      }
    } finally {
      this.busy.set(false);
    }
  }
}
