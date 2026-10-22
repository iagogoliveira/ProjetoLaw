import { CaseDeadlinesTab } from '../../deadlines/components/case-deadlines-tab';
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
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import type {
  CaseOutcome,
  RelationKind,
  RestrictionEntry,
  TimelineEntry,
} from '../../../api/types.gen';
import { CanDirective } from '../../../core/organization/can.directive';
import { formatDocument } from '../../../shared/br-format';
import { CaseDetailStore } from '../data/case-stores';
import { formatCnj, TIMELINE_GROUPS } from '../data/cases-format';
import { ClaimsTab } from './tabs/claims-tab';
import { RiskTab } from './tabs/risk-tab';
import { SettlementsTab } from './tabs/settlements-tab';
import { HearingsTab } from './tabs/hearings-tab';
import { MovementsTab } from '../../publications/components/movements-tab';

/** Ação de ciclo de vida aberta para confirmação. */
type PendingAction = 'close' | 'delete' | null;

/** Data ISO (`yyyy-MM-dd` ou com hora) em `dd/mm/aaaa`. */
function dateBr(value: string | null | undefined): string {
  return value ? value.substring(0, 10).split('-').reverse().join('/') : '—';
}

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

/**
 * Detalhe do processo (5E1): cabeçalho com a situação e as ações (editar, suspender, reativar, encerrar, reabrir,
 * excluir) e as abas Resumo, Partes e responsáveis, Hierarquia, Linha do tempo e Sigilo; Documentos, Prazos e
 * Financeiro ficam reservadas para as etapas deles. As abas de valores e acompanhamento têm stores próprias.
 */
@Component({
  selector: 'lf-case-detail-page',
  imports: [
    CaseDeadlinesTab,
    CanDirective,
    ClaimsTab,
    RiskTab,
    SettlementsTab,
    HearingsTab,
    MovementsTab,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [CaseDetailStore],
  templateUrl: './case-detail-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CaseDetailPage implements OnInit {
  readonly id = input.required<string>();

  protected readonly store = inject(CaseDetailStore);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  protected readonly formatCnj = formatCnj;
  protected readonly formatDocument = formatDocument;
  protected readonly dateBr = dateBr;
  protected readonly groups = TIMELINE_GROUPS;
  protected readonly outcomes: CaseOutcome[] = [
    'granted',
    'partiallyGranted',
    'denied',
    'settlement',
    'dismissed',
    'withdrawn',
    'other',
  ];
  protected readonly relationKinds: RelationKind[] = [
    'incident',
    'appeal',
    'enforcement',
    'attached',
  ];

  protected readonly pending = signal<PendingAction>(null);
  protected readonly closing = signal({
    reasonId: null as string | null,
    outcome: 'settlement' as CaseOutcome,
    finalAmount: '',
    closedOn: '',
    note: '',
  });
  protected readonly candidateTerm = signal('');
  protected readonly childId = signal<string | null>(null);
  protected readonly relationKind = signal<RelationKind>('appeal');
  protected readonly timelineFilter = signal<string[]>([]);
  protected readonly allowedUsers = signal<string[]>([]);
  protected readonly allowedTeams = signal<string[]>([]);
  protected readonly blockedUsers = signal<string[]>([]);
  protected readonly blockedTeams = signal<string[]>([]);

  protected readonly closed = computed(() => this.store.legalCase()?.status === 'closed');

  async ngOnInit(): Promise<void> {
    await this.store.load(this.id());
    await this.store.loadTimeline([]);
  }

  protected nameOf(
    list: 'practiceAreas' | 'branches' | 'phases' | 'actionTypes' | 'closingReasons' | 'teams',
    id: string | null,
  ): string {
    return (id && this.store.options()?.[list].find((o) => o.id === id)?.name) || '—';
  }

  protected memberName(userId: string | null): string {
    return (userId && this.store.options()?.members.find((m) => m.userId === userId)?.name) || '—';
  }

  protected amount(value: number | string | null): string {
    return value === null ? '—' : money.format(Number(value));
  }

  protected async confirmClose(): Promise<void> {
    const c = this.closing();
    if (!c.reasonId || !c.closedOn) {
      return;
    }

    const done = await this.store.close({
      reasonId: c.reasonId,
      outcome: c.outcome,
      finalAmount: c.finalAmount.trim()
        ? Number(c.finalAmount.replace(/\./g, '').replace(',', '.'))
        : null,
      closedOn: c.closedOn,
      note: c.note.trim() || null,
    });
    if (done) {
      this.pending.set(null);
      await this.store.loadTimeline(this.timelineFilter());
    }
  }

  protected async changeStatus(status: 'active' | 'suspended' | 'reopen'): Promise<void> {
    const done =
      status === 'reopen' ? await this.store.reopen() : await this.store.setStatus(status);
    if (done) {
      await this.store.loadTimeline(this.timelineFilter());
    }
  }

  protected async confirmDelete(): Promise<void> {
    if (await this.store.remove()) {
      await this.router.navigate(['/processos']);
    }
  }

  protected async link(): Promise<void> {
    const child = this.childId();
    if (child && (await this.store.link(child, this.relationKind()))) {
      this.childId.set(null);
      this.candidateTerm.set('');
    }
  }

  protected async filterTimeline(kinds: string[]): Promise<void> {
    this.timelineFilter.set(kinds);
    await this.store.loadTimeline(kinds);
  }

  protected async openRestrictions(): Promise<void> {
    await this.store.loadRestrictions();
    const r = this.store.restrictions();
    if (!r) {
      return;
    }

    const users = (list: RestrictionEntry[]) => list.filter((e) => e.userId).map((e) => e.userId!);
    const teams = (list: RestrictionEntry[]) => list.filter((e) => e.teamId).map((e) => e.teamId!);
    this.allowedUsers.set(users(r.allowed));
    this.allowedTeams.set(teams(r.allowed));
    this.blockedUsers.set(users(r.blocked));
    this.blockedTeams.set(teams(r.blocked));
  }

  protected saveRestrictions(): Promise<boolean> {
    const entries = (users: string[], teams: string[]): RestrictionEntry[] => [
      ...users.map((userId) => ({ userId, teamId: null })),
      ...teams.map((teamId) => ({ userId: null, teamId })),
    ];
    return this.store.saveRestrictions(
      entries(this.allowedUsers(), this.allowedTeams()),
      entries(this.blockedUsers(), this.blockedTeams()),
    );
  }

  /** A chave de tradução e os valores de um acontecimento (datas, valores e situações já no formato da tela). */
  protected describe(entry: TimelineEntry): { key: string; params: Record<string, string> } {
    const data = entry.data;
    const status = (value: string | undefined) =>
      value
        ? this.transloco.translate('cases.status.' + value.charAt(0).toLowerCase() + value.slice(1))
        : '';
    const params: Record<string, string> = {
      ...data,
      from:
        data['from'] && data['from'].includes('T') ? dateBr(data['from']) : status(data['from']),
      to: data['to'] && data['to'].includes('T') ? dateBr(data['to']) : status(data['to']),
      scheduledAt: dateBr(data['scheduledAt']),
      amount: data['amount'] ? money.format(Number(data['amount'])) : '—',
      estimatedLoss: data['estimatedLoss'] ? money.format(Number(data['estimatedLoss'])) : '—',
      old: data['old'] ? money.format(Number(data['old'])) : '—',
      new: data['new'] ? money.format(Number(data['new'])) : '—',
      by: this.memberName(data['by'] ?? null),
    };
    return { key: 'cases.timeline.' + entry.kind.replace('litigation.', ''), params };
  }
}
