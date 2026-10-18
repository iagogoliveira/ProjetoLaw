import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { ConflictSide } from '../../../api/types.gen';
import { ConflictStore } from '../data/conflict-store';
import { digitsOf, formatDocument } from '../data/people-format';

/**
 * Verificação de conflito de interesses (RF-07) antes de abrir cliente ou caso: nome, documento, lado e e-mails de
 * contato. Mostra o risco e os achados; com alerta, prosseguir exige justificativa, que fica gravada para o relatório.
 */
@Component({
  selector: 'lf-conflict-check-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    ReactiveFormsModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [ConflictStore],
  template: `
    <section class="page" *transloco="let t">
      <div class="inline header">
        <h1>{{ t('conflicts.title') }}</h1>
        <a mat-button routerLink="relatorio">{{ t('conflicts.report') }}</a>
      </div>
      <p class="hint">{{ t('conflicts.intro') }}</p>

      <form [formGroup]="form" (ngSubmit)="check()" novalidate>
        <mat-radio-group
          formControlName="side"
          class="inline"
          [attr.aria-label]="t('conflicts.side')"
        >
          <mat-radio-button value="client">{{ t('conflicts.sides.client') }}</mat-radio-button>
          <mat-radio-button value="opponent">{{ t('conflicts.sides.opponent') }}</mat-radio-button>
        </mat-radio-group>
        <mat-form-field appearance="outline">
          <mat-label>{{ t('conflicts.name') }}</mat-label>
          <input matInput formControlName="name" required />
          <mat-error>{{ t('errors.crm.name_required') }}</mat-error>
        </mat-form-field>
        <div class="inline">
          <mat-form-field appearance="outline">
            <mat-label>{{ t('conflicts.document') }}</mat-label>
            <input matInput formControlName="document" inputmode="numeric" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ t('conflicts.emails') }}</mat-label>
            <input matInput formControlName="emails" />
            <mat-hint>{{ t('conflicts.emailsHint') }}</mat-hint>
          </mat-form-field>
        </div>
        <div class="actions">
          <button mat-flat-button type="submit" [disabled]="store.busy()">
            {{ t('conflicts.check') }}
          </button>
        </div>
      </form>

      @if (store.errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }

      @if (store.result(); as result) {
        <section class="panel" aria-labelledby="result-title">
          <h2 id="result-title">
            {{ t('conflicts.result') }}:
            <span [class]="'risk-' + result.risk">{{ t('conflicts.risks.' + result.risk) }}</span>
          </h2>
          <p class="hint">
            {{ result.queryName }}
            @if (result.queryDocument) {
              · {{ format(result.queryDocument) }}
            }
            · {{ t('conflicts.sides.' + result.side) }}
          </p>
          @if (result.hits.length === 0) {
            <p>{{ t('conflicts.noHits') }}</p>
          } @else {
            <table>
              <thead>
                <tr>
                  <th scope="col">{{ t('conflicts.risk') }}</th>
                  <th scope="col">{{ t('conflicts.match') }}</th>
                  <th scope="col">{{ t('conflicts.reason') }}</th>
                </tr>
              </thead>
              <tbody>
                @for (hit of result.hits; track $index) {
                  <tr>
                    <td [class]="'risk-' + hit.risk">{{ t('conflicts.risks.' + hit.risk) }}</td>
                    <td>
                      @if (hit.personId) {
                        <a [routerLink]="['/pessoas', hit.personId]">{{ hit.matchedName }}</a>
                      } @else {
                        {{ hit.matchedName }}
                      }
                      @if (hit.caseNumber) {
                        <span class="hint"> · {{ hit.caseNumber }}</span>
                      }
                    </td>
                    <td>
                      {{ t('conflicts.reasons.' + hit.reason) }}
                      @if (hit.score !== null) {
                        ({{ percent(hit.score) }}%)
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }

          @if (result.proceeded) {
            <p class="status" role="status">{{ t('conflicts.proceeded') }}</p>
            @if (result.justification) {
              <p>{{ t('conflicts.justification') }}: {{ result.justification }}</p>
            }
            <div class="actions">
              <button mat-button type="button" (click)="store.reset()">
                {{ t('conflicts.newCheck') }}
              </button>
            </div>
          } @else {
            @if (result.risk !== 'none') {
              <mat-form-field appearance="outline">
                <mat-label>{{ t('conflicts.justification') }}</mat-label>
                <textarea
                  matInput
                  rows="3"
                  maxlength="2000"
                  required
                  [value]="justification()"
                  (input)="justification.set($any($event.target).value)"
                ></textarea>
                <mat-hint>{{ t('conflicts.justificationHint') }}</mat-hint>
              </mat-form-field>
            }
            <div class="actions">
              <button
                mat-flat-button
                type="button"
                [disabled]="store.busy() || (result.risk !== 'none' && !justification().trim())"
                (click)="proceed()"
              >
                {{ t('conflicts.proceed') }}
              </button>
              <button mat-button type="button" (click)="store.reset()">
                {{ t('conflicts.newCheck') }}
              </button>
            </div>
          }
        </section>
      }
    </section>
  `,
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConflictCheckPage {
  protected readonly store = inject(ConflictStore);
  protected readonly format = formatDocument;
  protected readonly justification = signal('');

  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly form = this.fb.group({
    side: this.fb.control<ConflictSide>('client'),
    name: this.fb.control('', [Validators.required, Validators.maxLength(300)]),
    document: this.fb.control(''),
    emails: this.fb.control(''),
  });

  protected percent(score: number | string): string {
    return (Number(score) * 100).toFixed(0);
  }

  protected async check(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const v = this.form.getRawValue();
    this.justification.set('');
    await this.store.check({
      name: v.name.trim(),
      document: digitsOf(v.document) || null,
      side: v.side,
      contactEmails: v.emails
        .split(/[,;\s]+/)
        .map((e) => e.trim())
        .filter((e) => e.length > 0),
    });
  }

  protected proceed(): Promise<boolean> {
    return this.store.proceed(this.justification());
  }
}
