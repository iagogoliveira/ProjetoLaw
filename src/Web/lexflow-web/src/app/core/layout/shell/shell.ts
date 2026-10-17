import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatSelectModule } from '@angular/material/select';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AuthSession } from '../../auth/auth-session';
import { AccessStore } from '../../organization/access-store';
import { CanDirective } from '../../organization/can.directive';

/**
 * Layout das rotas internas: barra superior com o escritório ativo (e a troca, para quem está em mais de um), o
 * usuário e "Sair"; navegação lateral filtrada por permissão (`*lfCan`) e conteúdo roteado.
 */
@Component({
  selector: 'lf-shell',
  imports: [
    CanDirective,
    MatButtonModule,
    MatListModule,
    MatSelectModule,
    MatToolbarModule,
    MatSidenavModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    TranslocoDirective,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell implements OnInit {
  protected readonly session = inject(AuthSession);
  protected readonly access = inject(AccessStore);

  ngOnInit(): Promise<void> {
    return this.access.ensureLoaded();
  }

  /** Troca de organização (RF-02): recarrega a página para nenhuma tela ficar com dados do escritório anterior. */
  async switchTo(organizationId: string): Promise<void> {
    if (await this.access.switchTo(organizationId)) {
      window.location.assign('/');
    }
  }
}
