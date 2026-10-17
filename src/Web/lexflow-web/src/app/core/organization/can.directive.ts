import { Directive, effect, inject, input, TemplateRef, ViewContainerRef } from '@angular/core';
import { AccessStore } from './access-store';

/**
 * Mostra o conteúdo só para quem tem a permissão na organização ativa (seção 5.2, item 5):
 * `<button *lfCan="'settings.users.manage'">`. A UI só esconde; quem bloqueia é o servidor.
 */
@Directive({ selector: '[lfCan]' })
export class CanDirective {
  private readonly access = inject(AccessStore);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);
  private shown = false;

  readonly lfCan = input.required<string>();

  constructor() {
    effect(() => {
      const allowed = this.access.permissions().has(this.lfCan());
      if (allowed && !this.shown) {
        this.container.createEmbeddedView(this.template);
      } else if (!allowed && this.shown) {
        this.container.clear();
      }
      this.shown = allowed;
    });
  }
}
