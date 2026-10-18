import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { PersonRoleKind } from '../../../api/types.gen';
import { CanDirective } from '../../../core/organization/can.directive';
import { formatDocument, PERSON_ROLES } from '../data/people-format';
import { PeopleStore } from '../data/people-store';

/** Espera entre a última tecla e a busca. */
const SEARCH_DELAY_MS = 300;

/**
 * Pessoas (RF-06): busca por nome sem acentos (aproximada) ou documento, filtro por papel e "carregar mais" na lista
 * em ordem alfabética.
 */
@Component({
  selector: 'lf-people-list-page',
  imports: [
    CanDirective,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [PeopleStore],
  template: `
    <section class="page" *transloco="let t">
      <div class="inline header">
        <h1>{{ t('people.title') }}</h1>
        <a *lfCan="'crm.people.create'" mat-flat-button routerLink="nova">{{ t('people.new') }}</a>
      </div>

      <div class="inline" role="search">
        <mat-form-field appearance="outline">
          <mat-label>{{ t('people.search') }}</mat-label>
          <input
            matInput
            type="search"
            [ngModel]="term()"
            (ngModelChange)="onTerm($event)"
            [placeholder]="t('people.searchHint')"
          />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ t('people.role') }}</mat-label>
          <mat-select [ngModel]="role()" (ngModelChange)="onRole($event)">
            <mat-option [value]="null">{{ t('people.allRoles') }}</mat-option>
            @for (option of roles; track option) {
              <mat-option [value]="option">{{ t('people.roles.' + option) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      @if (store.errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }

      @if (store.items().length === 0 && !store.loading()) {
        <p class="empty">{{ t(term() ? 'people.noResults' : 'people.empty') }}</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th scope="col">{{ t('people.name') }}</th>
              <th scope="col">{{ t('people.document') }}</th>
              <th scope="col">{{ t('people.rolesLabel') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (person of store.items(); track person.id) {
              <tr>
                <td>
                  <a [routerLink]="person.id">{{ person.name }}</a>
                  @if (person.alternateName) {
                    <span class="hint"> ({{ person.alternateName }})</span>
                  }
                </td>
                <td>{{ format(person.document) || '—' }}</td>
                <td>
                  @for (r of person.roles; track r) {
                    <span class="chip">{{ t('people.roles.' + r) }}</span>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (store.nextCursor()) {
        <button mat-button type="button" [disabled]="store.loading()" (click)="store.loadMore()">
          {{ t('people.loadMore') }}
        </button>
      }
    </section>
  `,
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PeopleListPage implements OnInit, OnDestroy {
  protected readonly store = inject(PeopleStore);
  protected readonly roles = PERSON_ROLES;
  protected readonly term = signal('');
  protected readonly role = signal<PersonRoleKind | null>(null);
  protected readonly format = formatDocument;
  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): Promise<void> {
    return this.store.search('', null);
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  protected onTerm(value: string): void {
    this.term.set(value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.store.search(value, this.role()), SEARCH_DELAY_MS);
  }

  protected onRole(value: PersonRoleKind | null): void {
    this.role.set(value);
    clearTimeout(this.timer);
    void this.store.search(this.term(), value);
  }
}
