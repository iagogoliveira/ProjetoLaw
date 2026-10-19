import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoDirective } from '@jsverse/transloco';
import { NumberingStore } from '../data/case-stores';

/**
 * Numeração interna dos processos (configurações, aba própria): o formato com os marcadores {AREA}, {ANO} e
 * {SEQ:00000} e a sigla de cada área. Vale para os cadastros seguintes.
 */
@Component({
  selector: 'lf-numbering-page',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, TranslocoDirective],
  providers: [NumberingStore],
  template: `
    <section class="page" *transloco="let t">
      <h2>{{ t('cases.numbering.title') }}</h2>
      <p class="hint">{{ t('cases.numbering.hint') }}</p>
      @if (store.errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }
      @if (store.numbering(); as numbering) {
        <div class="form">
          <mat-form-field appearance="outline">
            <mat-label>{{ t('cases.numbering.format') }}</mat-label>
            <input matInput [ngModel]="format()" (ngModelChange)="format.set($event)" />
          </mat-form-field>
          <p class="hint">{{ t('cases.numbering.example', { example: numbering.example }) }}</p>
          @if (store.options(); as options) {
            <fieldset>
              <legend>{{ t('cases.numbering.areaCodes') }}</legend>
              @for (area of options.practiceAreas; track area.id) {
                <mat-form-field appearance="outline">
                  <mat-label>{{ area.name }}</mat-label>
                  <input
                    matInput
                    maxlength="10"
                    [ngModel]="codes()[area.id] ?? ''"
                    (ngModelChange)="setCode(area.id, $event)"
                  />
                </mat-form-field>
              } @empty {
                <p class="hint">{{ t('cases.numbering.noAreas') }}</p>
              }
            </fieldset>
          }
          <div class="actions">
            <button mat-flat-button type="button" [disabled]="store.busy()" (click)="save()">
              {{ t('cases.numbering.save') }}
            </button>
          </div>
          @if (store.saved()) {
            <p class="status" role="status">{{ t('cases.numbering.saved') }}</p>
          }
        </div>
      }
    </section>
  `,
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NumberingPage implements OnInit {
  protected readonly store = inject(NumberingStore);
  protected readonly format = signal('');
  protected readonly codes = signal<Record<string, string>>({});

  async ngOnInit(): Promise<void> {
    await this.store.load();
    const numbering = this.store.numbering();
    if (numbering) {
      this.format.set(numbering.format);
      this.codes.set({ ...numbering.areaCodes });
    }
  }

  protected setCode(areaId: string, code: string): void {
    this.codes.update((codes) => ({ ...codes, [areaId]: code }));
  }

  protected save(): Promise<boolean> {
    const codes = Object.fromEntries(
      Object.entries(this.codes()).filter(([, code]) => code.trim()),
    );
    return this.store.save(this.format(), codes);
  }
}
