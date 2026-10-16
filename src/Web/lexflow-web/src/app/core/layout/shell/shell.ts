import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatSidenav, MatSidenavContainer, MatSidenavContent } from '@angular/material/sidenav';
import { MatToolbar } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';

/** Layout base da aplicação: barra superior, menu lateral (vazio na Etapa 0) e área de conteúdo. */
@Component({
  selector: 'lf-shell',
  imports: [
    MatToolbar,
    MatIconButton,
    MatIcon,
    MatSidenav,
    MatSidenavContainer,
    MatSidenavContent,
    RouterOutlet,
    TranslocoDirective,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  protected readonly navOpen = signal(true);

  protected toggleNav(): void {
    this.navOpen.update((open) => !open);
  }
}
