import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { TemplatesStore } from '../data/templates-store';

/**
 * Modelos de proposta (aba das configurações): os modelos DOCX do escritório, os marcadores aceitos, o modelo padrão
 * para baixar e adaptar, o envio de um modelo novo (antivírus, formato e marcadores conferidos no servidor) e a
 * exclusão.
 */
@Component({
  selector: 'lf-proposal-templates-page',
  imports: [
    CanDirective,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoDirective,
  ],
  providers: [TemplatesStore],
  template: `
    <section class="page" *transloco="let t">
      <div class="inline header">
        <h2>{{ t('templates.title') }}</h2>
        <button mat-stroked-button type="button" (click)="store.downloadDefault()">
          {{ t('templates.downloadDefault') }}
        </button>
      </div>
      <p class="hint">{{ t('templates.intro') }}</p>
      <p class="hint">
        {{ t('templates.markers') }}:
        @for (marker of store.markers(); track marker) {
          <code class="chip">{{ '{{' + marker + '}}' }}</code>
        }
      </p>
      <p class="hint">{{ t('templates.itemsHint') }}</p>

      @if (store.errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }
      @if (store.uploaded()) {
        <p class="status" role="status">{{ t('templates.uploaded') }}</p>
      }

      @if (store.templates().length === 0) {
        <p class="empty">{{ t('templates.empty') }}</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th scope="col">{{ t('templates.name') }}</th>
              <th scope="col">{{ t('templates.file') }}</th>
              <th scope="col">{{ t('templates.createdAt') }}</th>
              <th scope="col">
                <span class="cdk-visually-hidden">{{ t('settings.actions') }}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            @for (template of store.templates(); track template.id) {
              <tr>
                <td>{{ template.name }}</td>
                <td>{{ template.fileName }} ({{ kilobytes(template.size) }} KB)</td>
                <td>{{ template.createdAt | date: 'shortDate' }}</td>
                <td>
                  <button
                    *lfCan="'crm.proposals.manage'"
                    mat-button
                    type="button"
                    [disabled]="store.busy()"
                    (click)="store.remove(template.id)"
                  >
                    {{ t('people.form.remove') }}
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      <form *lfCan="'crm.proposals.manage'" class="form" (ngSubmit)="upload()">
        <h3>{{ t('templates.upload') }}</h3>
        <mat-form-field appearance="outline">
          <mat-label>{{ t('templates.name') }}</mat-label>
          <input
            matInput
            name="name"
            required
            [ngModel]="name()"
            (ngModelChange)="name.set($event)"
          />
        </mat-form-field>
        <label class="file">
          {{ t('templates.file') }}
          <input
            type="file"
            accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            (change)="pick($event)"
          />
        </label>
        <button
          mat-flat-button
          type="submit"
          [disabled]="store.busy() || !name().trim() || !file()"
        >
          {{ t('templates.send') }}
        </button>
      </form>
    </section>
  `,
  styleUrl: '../../../shared/ui/page.scss',
  styles: `
    .file {
      display: grid;
      gap: var(--lf-space-xs, 4px);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProposalTemplatesPage implements OnInit {
  protected readonly store = inject(TemplatesStore);
  protected readonly name = signal('');
  protected readonly file = signal<File | null>(null);

  ngOnInit(): Promise<void> {
    return this.store.load();
  }

  protected kilobytes(size: number | string): string {
    return (Number(size) / 1024).toFixed(0);
  }

  protected pick(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }

  protected async upload(): Promise<void> {
    const file = this.file();
    if (file && (await this.store.upload(this.name().trim(), file))) {
      this.name.set('');
    }
  }
}
