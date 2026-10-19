import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import type { FieldErrors } from '../../../../core/data/api-result';

@Component({
  selector: 'lf-value-errors',
  imports: [TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      @if (errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }
      @for (code of codes(); track code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }
    </ng-container>
  `,
  styles: '.error { color: var(--mat-sys-error); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ValueErrors {
  readonly errorCode = input<string | null>(null);
  readonly fields = input<FieldErrors>();
  protected readonly codes = computed(() => [
    ...new Set(Object.values(this.fields() ?? {}).flatMap((e) => e.map((v) => v.code))),
  ]);
}
