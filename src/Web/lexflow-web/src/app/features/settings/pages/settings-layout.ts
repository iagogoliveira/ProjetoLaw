import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';

/** Configurações do escritório: uma aba por tela, cada uma visível só para quem tem a permissão dela. */
@Component({
  selector: 'lf-settings-layout',
  imports: [
    CanDirective,
    MatTabsModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    TranslocoDirective,
  ],
  template: `
    <ng-container *transloco="let t">
      <h1>{{ t('settings.title') }}</h1>
      <nav mat-tab-nav-bar [tabPanel]="panel" [attr.aria-label]="t('settings.sections')">
        @for (tab of tabs; track tab.path) {
          <a
            *lfCan="tab.permission"
            mat-tab-link
            [routerLink]="tab.path"
            routerLinkActive
            #active="routerLinkActive"
            [active]="active.isActive"
            >{{ t('settings.tabs.' + tab.label) }}</a
          >
        }
      </nav>
      <mat-tab-nav-panel #panel>
        <div class="panel">
          <router-outlet />
        </div>
      </mat-tab-nav-panel>
    </ng-container>
  `,
  styles: `
    h1 {
      margin-top: 0;
    }

    .panel {
      padding-top: var(--lf-space-md);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsLayout {
  protected readonly tabs = [
    { path: 'escritorio', label: 'organization', permission: 'settings.organization.manage' },
    { path: 'usuarios', label: 'users', permission: 'settings.users.read' },
    { path: 'perfis', label: 'roles', permission: 'settings.users.read' },
    { path: 'estrutura', label: 'structure', permission: 'settings.structure.manage' },
    { path: 'campos', label: 'customFields', permission: 'settings.custom_fields.read' },
  ] as const;
}
