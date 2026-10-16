import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';

/** Layout principal: barra superior, navegação lateral (vazia até a primeira feature) e conteúdo roteado. */
@Component({
  selector: 'lf-shell',
  imports: [MatToolbarModule, MatSidenavModule, RouterOutlet, TranslocoDirective],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {}
