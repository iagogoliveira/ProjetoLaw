import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'lf-publication-errors',
  template: `@if (code(); as error) {
    <p role="alert" class="error">{{ message(error) }}</p>
  }`,
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicationErrors {
  readonly code = input<string | null>(null);
  private readonly i18n = inject(TranslocoService);
  protected message(code: string): string {
    const key = `publications.errors.${code.replaceAll('.', '_')}`;
    const message = this.i18n.translate(key);
    return message === key ? this.i18n.translate('publications.errors.generic') : message;
  }
}
