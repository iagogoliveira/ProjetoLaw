import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { AssignmentRole, CaseBatchAction, CaseSummary } from '../../../api/types.gen';
import { CanDirective } from '../../../core/organization/can.directive';
import { CaseFilters, NO_FILTERS } from '../data/cases-api';
import { formatCnj } from '../data/cases-format';
import { CASE_COLUMNS, CaseColumn, CasesListStore } from '../data/cases-list-store';

/** Espera entre a última tecla e a busca. */
const SEARCH_DELAY_MS = 300;

/**
 * Processos (RF-09, 5E1): busca por pasta ou CNJ, filtros, filtros salvos e colunas (preferências do usuário),
 * "carregar mais" e as operações em lote sobre os selecionados.
 */
@Component({
  selector: 'lf-cases-list-page',
  imports: [
    CanDirective,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [CasesListStore],
  templateUrl: './cases-list-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CasesListPage implements OnInit, OnDestroy {
  protected readonly store = inject(CasesListStore);
  protected readonly allColumns = CASE_COLUMNS;
  protected readonly statuses = ['active', 'suspended', 'closed'] as const;
  protected readonly kinds = ['judicial', 'administrative', 'extrajudicial'] as const;
  protected readonly actions: CaseBatchAction[] = [
    'redistribute',
    'changePhase',
    'suspend',
    'reactivate',
  ];
  protected readonly roles: AssignmentRole[] = ['lawyer', 'partner'];
  protected readonly formatCnj = formatCnj;

  protected readonly filters = signal<CaseFilters>(NO_FILTERS);
  protected readonly filterName = signal('');
  protected readonly action = signal<CaseBatchAction | null>(null);
  protected readonly fromUser = signal<string | null>(null);
  protected readonly toUser = signal<string | null>(null);
  protected readonly role = signal<AssignmentRole>('lawyer');
  protected readonly phase = signal<string | null>(null);
  protected readonly note = signal('');

  protected readonly canRunBatch = computed(() => {
    const action = this.action();
    return (
      this.store.selected().length > 0 &&
      action !== null &&
      (action !== 'redistribute' || this.toUser() !== null) &&
      (action !== 'changePhase' || this.phase() !== null)
    );
  });

  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): Promise<void> {
    return this.store.init();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  protected has(column: CaseColumn): boolean {
    return this.store.columns().includes(column);
  }

  protected nameOf(list: 'practiceAreas' | 'phases', id: string | null): string {
    return (id && this.store.options()?.[list].find((o) => o.id === id)?.name) || '—';
  }

  protected onSearch(q: string): void {
    this.filters.update((f) => ({ ...f, q: q || null }));
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.store.search(this.filters()), SEARCH_DELAY_MS);
  }

  protected setFilter(key: keyof CaseFilters, value: string | null): void {
    this.filters.update((f) => ({ ...f, [key]: value }));
    clearTimeout(this.timer);
    void this.store.search(this.filters());
  }

  protected clearFilters(): void {
    this.filters.set(NO_FILTERS);
    void this.store.search(NO_FILTERS);
  }

  protected applySaved(name: string | null): void {
    const saved = this.store.saved().find((s) => s.name === name);
    if (saved) {
      this.filters.set({ ...NO_FILTERS, ...saved.filters });
      void this.store.search(this.filters());
    }
  }

  protected async saveFilter(): Promise<void> {
    if (this.filterName().trim() && (await this.store.saveFilter(this.filterName()))) {
      this.filterName.set('');
    }
  }

  protected async runBatch(): Promise<void> {
    const action = this.action();
    if (!action) {
      return;
    }

    const done = await this.store.runBatch({
      action,
      fromUserId: this.fromUser(),
      toUserId: this.toUser(),
      role: this.role(),
      phaseId: this.phase(),
      note: this.note().trim() || null,
    });
    if (done) {
      this.action.set(null);
      this.note.set('');
    }
  }

  protected folderOf(id: string): string {
    return this.store.items().find((c: CaseSummary) => c.id === id)?.internalFolder ?? id;
  }
}
