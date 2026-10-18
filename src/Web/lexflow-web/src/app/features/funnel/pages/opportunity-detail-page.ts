import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { formatDocument } from '../../../shared/br-format';
import { OpportunityStore } from '../data/opportunity-store';

/** Ação aberta para confirmação. */
type PendingAction = 'lose' | 'delete' | null;

/**
 * Detalhe da oportunidade: interessado, origem, valor e responsável; a verificação de conflito do interessado (exigida
 * para aceitar a proposta), perder com motivo, reabrir, excluir e as propostas, com o atalho para uma nova.
 */
@Component({
  selector: 'lf-opportunity-detail-page',
  imports: [
    CanDirective,
    CurrencyPipe,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [OpportunityStore],
  templateUrl: './opportunity-detail-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OpportunityDetailPage implements OnInit {
  readonly id = input.required<string>();

  protected readonly store = inject(OpportunityStore);
  protected readonly format = formatDocument;
  protected readonly pending = signal<PendingAction>(null);
  protected readonly reasonId = signal<string | null>(null);
  protected readonly note = signal('');
  protected readonly justification = signal('');

  private readonly router = inject(Router);

  ngOnInit(): Promise<void> {
    return this.store.load(this.id());
  }

  protected open(action: PendingAction): void {
    this.reasonId.set(null);
    this.note.set('');
    this.pending.set(action);
  }

  protected async confirmLose(): Promise<void> {
    const reason = this.reasonId();
    if (reason && (await this.store.lose(reason, this.note().trim() || null))) {
      this.pending.set(null);
    }
  }

  protected async confirmDelete(): Promise<void> {
    if (await this.store.remove()) {
      await this.router.navigate(['/funil']);
    }
  }

  protected async proceed(): Promise<void> {
    if (await this.store.proceedConflict(this.justification())) {
      this.justification.set('');
    }
  }
}
