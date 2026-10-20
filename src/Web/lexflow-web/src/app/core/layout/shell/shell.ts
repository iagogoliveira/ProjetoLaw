import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  Injector,
  OnInit,
  OnDestroy,
} from '@angular/core';
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
import { NotificationBell } from '../../../features/publications/components/notification-bell';
import { PendingBadge } from '../../../features/publications/components/pending-badge';
import { PublicationsLiveStore } from '../../../features/publications/data/publications-live-store';

/**
 * Layout das rotas internas: barra superior com o escritório ativo (e a troca, para quem está em mais de um), o
 * usuário e "Sair"; navegação lateral filtrada por permissão (`*lfCan`) e conteúdo roteado.
 */
@Component({
  selector: 'lf-shell',
  imports: [
    CanDirective,
    NotificationBell,
    PendingBadge,
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
export class Shell implements OnInit, OnDestroy {
  protected readonly session = inject(AuthSession);
  protected readonly access = inject(AccessStore);

  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private live?: InstanceType<typeof PublicationsLiveStore>;

  async ngOnInit(): Promise<void> {
    await this.access.ensureLoaded();
    if (this.destroyRef.destroyed) return;
    if (this.access.can('identity.profile.read') || this.access.can('publications.read')) {
      this.live = this.injector.get(PublicationsLiveStore);
      this.live.start();
    }
  }

  ngOnDestroy(): void {
    this.live?.stop();
  }

  /** Troca de organização (RF-02): recarrega a página para nenhuma tela ficar com dados do escritório anterior. */
  async switchTo(organizationId: string): Promise<void> {
    if (await this.access.switchTo(organizationId)) {
      window.location.assign('/');
    }
  }
}
