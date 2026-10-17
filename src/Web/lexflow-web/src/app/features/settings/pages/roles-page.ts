import { ChangeDetectionStrategy, Component, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { RolesStore } from '../data/roles-store';

/**
 * Perfis com a matriz de permissões (seção 7.2): escolhe o perfil, marca as permissões de cada grupo e salva. Vale
 * para os membros em até 5 s, sem novo login. O Administrador do escritório tem sempre todas e não é editado.
 */
@Component({
  selector: 'lf-roles-page',
  imports: [
    CanDirective,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatListModule,
    TranslocoDirective,
  ],
  providers: [RolesStore],
  template: `
    <section class="page layout" *transloco="let t">
      <div class="roles">
        <h2>{{ t('settings.roles.title') }}</h2>
        <mat-selection-list [multiple]="false" [attr.aria-label]="t('settings.roles.title')">
          @for (role of store.roles(); track role.id) {
            <mat-list-option
              [value]="role.id"
              [selected]="role.id === store.selectedId()"
              (click)="store.select(role.id)"
            >
              {{ role.name }}
            </mat-list-option>
          }
        </mat-selection-list>
        <form class="form" *lfCan="'settings.roles.manage'" (ngSubmit)="create()">
          <mat-form-field appearance="outline">
            <mat-label>{{ t('settings.roles.newName') }}</mat-label>
            <input matInput name="newRole" [(ngModel)]="newName" maxlength="100" />
          </mat-form-field>
          <button mat-stroked-button type="submit" [disabled]="store.busy() || !newName().trim()">
            {{ t('settings.roles.create') }}
          </button>
        </form>
      </div>

      @if (store.selected(); as role) {
        <div class="matrix">
          @if (store.errorCode(); as code) {
            <p class="error" role="alert">{{ t('errors.' + code) }}</p>
          }
          @if (store.saved()) {
            <p class="status" role="status">{{ t('settings.saved') }}</p>
          }
          <div class="inline">
            <mat-form-field appearance="outline">
              <mat-label>{{ t('settings.roles.name') }}</mat-label>
              <input
                matInput
                name="roleName"
                [(ngModel)]="name"
                [disabled]="!store.canManage()"
                maxlength="100"
              />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ t('settings.roles.description') }}</mat-label>
              <input
                matInput
                name="roleDescription"
                [(ngModel)]="description"
                [disabled]="!store.canManage()"
                maxlength="500"
              />
            </mat-form-field>
            <div class="actions" *lfCan="'settings.roles.manage'">
              <button
                mat-button
                type="button"
                [disabled]="store.busy()"
                (click)="store.update(role.id, name().trim(), description().trim() || null)"
              >
                {{ t('settings.roles.rename') }}
              </button>
              <button
                mat-button
                type="button"
                [disabled]="store.busy() || role.systemKey === 'office_admin'"
                (click)="store.remove(role.id)"
              >
                {{ t('settings.roles.delete') }}
              </button>
            </div>
          </div>
          @if (role.systemKey === 'office_admin') {
            <p class="hint">{{ t('settings.roles.adminLocked') }}</p>
          }
          @for (group of store.groups(); track group.group) {
            <fieldset>
              <legend>{{ group.group }}</legend>
              @for (permission of group.permissions; track permission.name) {
                <mat-checkbox
                  [checked]="checked().has(permission.name)"
                  [disabled]="store.isLocked(role)"
                  (change)="toggle(permission.name, $event.checked)"
                  >{{ permission.description }}</mat-checkbox
                >
              }
            </fieldset>
          }
          @if (!store.isLocked(role)) {
            <button
              mat-flat-button
              type="button"
              [disabled]="store.busy()"
              (click)="store.setPermissions(role.id, [...checked()])"
            >
              {{ t('settings.roles.savePermissions') }}
            </button>
          }
        </div>
      }
    </section>
  `,
  styleUrl: './settings-page.scss',
  styles: `
    .layout {
      grid-template-columns: minmax(200px, 260px) 1fr;
      align-items: start;
    }

    .matrix {
      display: grid;
      gap: var(--lf-space-sm);
    }

    fieldset {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
    }

    legend {
      font: var(--mat-sys-title-small);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RolesPage implements OnInit {
  protected readonly store = inject(RolesStore);
  protected readonly checked = signal<ReadonlySet<string>>(new Set());
  protected readonly newName = signal('');
  protected readonly name = signal('');
  protected readonly description = signal('');

  constructor() {
    // Ao trocar de perfil, a matriz e os campos mostram o perfil escolhido.
    effect(() => {
      const role = this.store.selected();
      this.checked.set(new Set(role?.permissions ?? []));
      this.name.set(role?.name ?? '');
      this.description.set(role?.description ?? '');
    });
  }

  ngOnInit(): Promise<void> {
    return this.store.load();
  }

  protected toggle(permission: string, on: boolean): void {
    this.checked.update((current) => {
      const next = new Set(current);
      if (on) {
        next.add(permission);
      } else {
        next.delete(permission);
      }
      return next;
    });
  }

  protected async create(): Promise<void> {
    if (await this.store.create(this.newName().trim(), [])) {
      this.newName.set('');
    }
  }
}
