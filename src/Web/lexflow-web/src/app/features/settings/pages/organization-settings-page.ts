import { ChangeDetectionStrategy, Component, effect, inject, OnInit } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoDirective } from '@jsverse/transloco';
import { OrganizationSettingsStore } from '../data/organization-settings-store';

/** Configurações do escritório: nome e verificação em duas etapas obrigatória. */
@Component({
  selector: 'lf-organization-settings-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    TranslocoDirective,
  ],
  providers: [OrganizationSettingsStore],
  template: `
    <section class="page" *transloco="let t">
      <h2>{{ t('settings.organization.title') }}</h2>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline">
          <mat-label>{{ t('settings.organization.name') }}</mat-label>
          <input matInput formControlName="name" maxlength="200" />
          @if (form.controls.name.invalid) {
            <mat-error>{{ t('errors.organization.name_required') }}</mat-error>
          }
        </mat-form-field>
        <mat-slide-toggle formControlName="mfaRequired">{{
          t('settings.organization.mfaRequired')
        }}</mat-slide-toggle>
        <p class="hint">{{ t('settings.organization.mfaHint') }}</p>
        @if (store.errorCode(); as code) {
          <p class="error" role="alert">{{ t('errors.' + code) }}</p>
        }
        @if (store.saved()) {
          <p class="status" role="status">{{ t('settings.saved') }}</p>
        }
        <button mat-flat-button type="submit" [disabled]="store.busy()">
          {{ t('settings.save') }}
        </button>
      </form>
    </section>
  `,
  styleUrl: './settings-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationSettingsPage implements OnInit {
  protected readonly store = inject(OrganizationSettingsStore);

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    mfaRequired: new FormControl(false, { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      const organization = this.store.organization();
      if (organization) {
        this.form.reset({ name: organization.name, mfaRequired: organization.mfaRequired });
      }
    });
  }

  ngOnInit(): Promise<void> {
    return this.store.load();
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, mfaRequired } = this.form.getRawValue();
    await this.store.save(name.trim(), mfaRequired);
  }
}
