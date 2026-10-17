import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Raiz: o shell só aparece nas rotas internas (com login); `/entrar` e o retorno do login ficam fora dele. */
@Component({
  selector: 'lf-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
