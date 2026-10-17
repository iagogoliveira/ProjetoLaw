import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { DataScope, MemberResponse, RoleAssignmentInput } from '../../../api/types.gen';
import { CanDirective } from '../../../core/organization/can.directive';
import { UsersStore } from '../data/users-store';

const SCOPES: readonly DataScope[] = ['own', 'team', 'practiceArea', 'branch', 'organization'];

/**
 * Usuários e convites: membros com os perfis e o escopo de dados de cada um (editar, remover) e os convites do
 * escritório (reenviar, cancelar). Quem só lê vê a lista sem as ações (`*lfCan`).
 */
@Component({
  selector: 'lf-users-page',
  imports: [
    CanDirective,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [UsersStore],
  template: `
    <section class="page" *transloco="let t">
      <div class="inline">
        <h2>{{ t('settings.users.members') }}</h2>
        <a *lfCan="'settings.users.manage'" mat-flat-button routerLink="../convites/novo">{{
          t('shell.invite')
        }}</a>
      </div>
      @if (store.errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }
      <table>
        <thead>
          <tr>
            <th scope="col">{{ t('settings.users.name') }}</th>
            <th scope="col">{{ t('settings.users.email') }}</th>
            <th scope="col">{{ t('settings.users.roles') }}</th>
            <th scope="col">
              <span class="cdk-visually-hidden">{{ t('settings.actions') }}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          @for (member of store.members(); track member.id) {
            <tr>
              <td>{{ member.fullName ?? '—' }}</td>
              <td>{{ member.email ?? '—' }}</td>
              <td>
                @if (editing() === member.id) {
                  <div class="form">
                    @for (row of draft(); track $index) {
                      <div class="inline">
                        <mat-form-field appearance="outline">
                          <mat-label>{{ t('settings.invite.role') }}</mat-label>
                          <mat-select [(ngModel)]="row.roleId">
                            @for (role of store.roles(); track role.id) {
                              <mat-option [value]="role.id">{{ role.name }}</mat-option>
                            }
                          </mat-select>
                        </mat-form-field>
                        <mat-form-field appearance="outline">
                          <mat-label>{{ t('settings.invite.scope') }}</mat-label>
                          <mat-select
                            [(ngModel)]="row.scope"
                            (ngModelChange)="row.scopeRefId = null"
                          >
                            @for (scope of scopes; track scope) {
                              <mat-option [value]="scope">{{
                                t('settings.scope.' + scope)
                              }}</mat-option>
                            }
                          </mat-select>
                        </mat-form-field>
                        @if (store.referencesFor(row.scope).length > 0) {
                          <mat-form-field appearance="outline">
                            <mat-label>{{ t('settings.users.scopeReference') }}</mat-label>
                            <mat-select [(ngModel)]="row.scopeRefId">
                              @for (
                                reference of store.referencesFor(row.scope);
                                track reference.id
                              ) {
                                <mat-option [value]="reference.id">{{ reference.name }}</mat-option>
                              }
                            </mat-select>
                          </mat-form-field>
                        }
                        <button mat-button type="button" (click)="removeRow($index)">
                          {{ t('settings.remove') }}
                        </button>
                      </div>
                    }
                    <div class="actions">
                      <button mat-button type="button" (click)="addRow()">
                        {{ t('settings.users.addRole') }}
                      </button>
                      <button
                        mat-flat-button
                        type="button"
                        [disabled]="store.busy()"
                        (click)="saveRoles(member.id)"
                      >
                        {{ t('settings.save') }}
                      </button>
                      <button mat-button type="button" (click)="editing.set(null)">
                        {{ t('settings.cancel') }}
                      </button>
                    </div>
                  </div>
                } @else {
                  @for (role of member.roles; track $index) {
                    <span class="role">
                      {{ store.roleNames().get(role.roleId) ?? '—' }} ({{
                        t('settings.scope.' + role.scope)
                      }})
                    </span>
                  }
                }
              </td>
              <td>
                @if (editing() !== member.id) {
                  <div class="actions" *lfCan="'settings.users.manage'">
                    <button mat-button type="button" (click)="edit(member)">
                      {{ t('settings.users.editRoles') }}
                    </button>
                    <button
                      mat-button
                      type="button"
                      [disabled]="store.busy()"
                      (click)="store.removeMember(member.id)"
                    >
                      {{ t('settings.users.remove') }}
                    </button>
                  </div>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>

      <h2>{{ t('settings.users.invitations') }}</h2>
      @if (store.invitations().length === 0) {
        <p class="empty">{{ t('settings.users.noInvitations') }}</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th scope="col">{{ t('settings.users.email') }}</th>
              <th scope="col">{{ t('settings.users.status') }}</th>
              <th scope="col">{{ t('settings.users.expiresAt') }}</th>
              <th scope="col">
                <span class="cdk-visually-hidden">{{ t('settings.actions') }}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            @for (invitation of store.invitations(); track invitation.id) {
              <tr>
                <td>{{ invitation.email }}</td>
                <td>{{ t('settings.users.invitationStatus.' + invitation.status) }}</td>
                <td>{{ invitation.expiresAt | date: 'short' }}</td>
                <td>
                  @if (invitation.status === 'pending' || invitation.status === 'expired') {
                    <div class="actions" *lfCan="'settings.users.manage'">
                      <button
                        mat-button
                        type="button"
                        [disabled]="store.busy()"
                        (click)="store.resend(invitation.id)"
                      >
                        {{ t('settings.users.resend') }}
                      </button>
                      <button
                        mat-button
                        type="button"
                        [disabled]="store.busy()"
                        (click)="store.revoke(invitation.id)"
                      >
                        {{ t('settings.users.revoke') }}
                      </button>
                    </div>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
  styleUrl: './settings-page.scss',
  styles: `
    .role {
      display: inline-block;
      margin-inline-end: var(--lf-space-sm);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage implements OnInit {
  protected readonly store = inject(UsersStore);
  protected readonly scopes = SCOPES;
  protected readonly editing = signal<string | null>(null);
  protected readonly draft = signal<RoleAssignmentInput[]>([]);

  ngOnInit(): Promise<void> {
    return this.store.load();
  }

  protected edit(member: MemberResponse): void {
    this.draft.set(
      member.roles.map((r) => ({ roleId: r.roleId, scope: r.scope, scopeRefId: r.scopeRefId })),
    );
    this.editing.set(member.id);
  }

  protected addRow(): void {
    this.draft.update((rows) => [
      ...rows,
      { roleId: this.store.roles()[0]?.id ?? '', scope: 'own', scopeRefId: null },
    ]);
  }

  protected removeRow(index: number): void {
    this.draft.update((rows) => rows.filter((_, i) => i !== index));
  }

  protected async saveRoles(memberId: string): Promise<void> {
    if (await this.store.assignRoles(memberId, this.draft())) {
      this.editing.set(null);
    }
  }
}
