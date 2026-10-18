import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { formatDocument, formatPostalCode } from '../data/people-format';
import { PersonDetailStore } from '../data/people-store';

/** Ação de ciclo de vida aberta para confirmação. */
type PendingAction = 'merge' | 'anonymize' | 'delete' | null;

/**
 * Detalhe da pessoa (RF-06), com as abas da Etapa 4: dados, contatos e endereços, e as de processos, documentos e
 * financeiro, que se preenchem com os módulos das próximas etapas. Ações: editar, unificar um duplicado, anonimizar
 * (LGPD, irreversível) e excluir, cada uma confirmada antes.
 */
@Component({
  selector: 'lf-person-detail-page',
  imports: [
    CanDirective,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatTabsModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [PersonDetailStore],
  templateUrl: './person-detail-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonDetailPage implements OnInit {
  readonly id = input.required<string>();

  protected readonly store = inject(PersonDetailStore);
  protected readonly format = formatDocument;
  protected readonly formatPostalCode = formatPostalCode;
  protected readonly pending = signal<PendingAction>(null);
  protected readonly duplicateId = signal<string | null>(null);
  protected readonly reason = signal('');

  protected readonly anonymized = computed(() => !!this.store.person()?.anonymizedAt);

  protected readonly groupName = computed(() => {
    const id = this.store.person()?.economicGroupId;
    return id ? (this.store.groups().find((g) => g.id === id)?.name ?? null) : null;
  });

  /** Campos customizados preenchidos, com o rótulo da definição e o nome das opções de lista. */
  protected readonly customFieldEntries = computed(() => {
    const options = new Map(
      this.store.tables().flatMap((table) => table.items.map((item) => [item.id, item.name])),
    );
    const labels = new Map(this.store.fields().map((field) => [field.key, field.label]));
    const show = (value: unknown): string => options.get(String(value)) ?? String(value);
    return Object.entries(this.store.person()?.customFields ?? {}).map(([key, value]) => ({
      key,
      label: labels.get(key) ?? key,
      value: Array.isArray(value) ? value.map(show).join(', ') : show(value),
    }));
  });

  private readonly router = inject(Router);

  ngOnInit(): Promise<void> {
    return this.store.load(this.id());
  }

  protected open(action: PendingAction): void {
    this.duplicateId.set(null);
    this.reason.set('');
    this.pending.set(action);
  }

  protected async confirmMerge(): Promise<void> {
    const duplicate = this.duplicateId();
    if (duplicate && (await this.store.merge(duplicate))) {
      this.pending.set(null);
    }
  }

  protected async confirmAnonymize(): Promise<void> {
    if (this.reason().trim() && (await this.store.anonymize(this.reason().trim()))) {
      this.pending.set(null);
    }
  }

  protected async confirmDelete(): Promise<void> {
    if (await this.store.remove()) {
      await this.router.navigate(['/pessoas']);
    }
  }
}
