import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { OpportunityStage, OpportunitySummary } from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { CanDirective } from '../../../core/organization/can.directive';
import { FunnelStore, OPEN_STAGES, STAGES } from '../data/funnel-store';

/**
 * Kanban do funil (RF-08): uma coluna por etapa, com quantidade e valor estimado. Os cartões das etapas abertas (e os
 * das perdidas, para reabrir) são arrastados entre lead, reunião e proposta; o seletor "Mover para" de cada cartão faz
 * o mesmo pelo teclado. Quem aprova vê a fila de propostas aguardando aprovação.
 */
@Component({
  selector: 'lf-funnel-page',
  imports: [
    CanDirective,
    CdkDrag,
    CdkDropList,
    CdkDropListGroup,
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [FunnelStore],
  template: `
    <section class="page" *transloco="let t">
      <div class="inline header">
        <h1>{{ t('funnel.title') }}</h1>
        <a *lfCan="'crm.opportunities.manage'" mat-flat-button routerLink="nova">{{
          t('funnel.new')
        }}</a>
      </div>

      @if (store.errorCode(); as code) {
        <p class="error" role="alert">{{ t('errors.' + code) }}</p>
      }

      @if (store.approvals().length > 0) {
        <section class="panel" aria-labelledby="approvals-title">
          <h2 id="approvals-title">{{ t('funnel.approvals') }}</h2>
          <ul>
            @for (proposal of store.approvals(); track proposal.id) {
              <li>
                <a [routerLink]="['propostas', proposal.id]">{{ proposal.title }}</a>
                — {{ proposal.prospectName }} · {{ +proposal.total | currency: 'BRL' }}
              </li>
            }
          </ul>
        </section>
      }

      <div
        class="board"
        cdkDropListGroup
        role="region"
        tabindex="0"
        [attr.aria-label]="t('funnel.board')"
      >
        @for (stage of stages; track stage) {
          <section class="column" [attr.aria-labelledby]="'stage-' + stage">
            <header>
              <h2 [id]="'stage-' + stage">{{ t('funnel.stages.' + stage) }}</h2>
              <p class="hint">
                {{ t('funnel.count', { count: store.totals().get(stage)?.count ?? 0 }) }} ·
                {{ store.totals().get(stage)?.value ?? 0 | currency: 'BRL' }}
              </p>
            </header>
            <div
              class="cards"
              cdkDropList
              [id]="stage"
              [cdkDropListData]="stage"
              [cdkDropListEnterPredicate]="acceptsDrop"
              (cdkDropListDropped)="drop($event)"
            >
              @for (card of store.columns()[stage].items; track card.id) {
                <article
                  class="card"
                  cdkDrag
                  [cdkDragData]="card"
                  [cdkDragDisabled]="!canMove(card)"
                  [attr.aria-label]="card.title"
                >
                  <a [routerLink]="card.id">{{ card.title }}</a>
                  <p>{{ card.prospectName }}</p>
                  <p class="hint">
                    @if (card.estimatedValue !== null) {
                      {{ +card.estimatedValue | currency: 'BRL' }}
                    }
                    @if (card.expectedCloseDate) {
                      · {{ card.expectedCloseDate | date: 'shortDate' }}
                    }
                  </p>
                  @if (canMove(card)) {
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="move">
                      <mat-label>{{ t('funnel.moveTo') }}</mat-label>
                      <mat-select
                        [value]="card.stage"
                        (selectionChange)="store.move(card.id, card.stage, $event.value)"
                      >
                        @for (target of openStages; track target) {
                          <mat-option [value]="target">{{
                            t('funnel.stages.' + target)
                          }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  }
                </article>
              } @empty {
                <p class="empty">{{ t('funnel.emptyColumn') }}</p>
              }
            </div>
            @if (store.columns()[stage].nextCursor) {
              <button mat-button type="button" (click)="store.loadMore(stage)">
                {{ t('people.loadMore') }}
              </button>
            }
          </section>
        }
      </div>
    </section>
  `,
  styleUrl: '../../../shared/ui/page.scss',
  styles: `
    .board {
      display: grid;
      grid-template-columns: repeat(5, minmax(220px, 1fr));
      gap: var(--lf-space-sm);
      overflow-x: auto;
      align-items: start;
    }

    .column {
      display: grid;
      gap: var(--lf-space-xs, 4px);
      padding: var(--lf-space-sm);
      border-radius: 8px;
      background: var(--mat-sys-surface-container-low);
    }

    .column h2 {
      font: var(--mat-sys-title-small);
    }

    .cards {
      display: grid;
      gap: var(--lf-space-xs, 4px);
      min-height: 80px;
    }

    .card {
      display: grid;
      gap: 2px;
      padding: var(--lf-space-sm);
      border-radius: 8px;
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
    }

    .card p {
      margin: 0;
    }

    .card[cdkDrag]:not(.cdk-drag-disabled) {
      cursor: grab;
    }

    .move {
      margin-top: var(--lf-space-xs, 4px);
    }

    .cdk-drag-preview {
      box-shadow: var(--mat-sys-level3);
    }

    .cdk-drag-placeholder {
      opacity: 0.4;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FunnelPage implements OnInit {
  protected readonly store = inject(FunnelStore);
  protected readonly stages = STAGES;
  protected readonly openStages = OPEN_STAGES;
  private readonly access = inject(AccessStore);

  ngOnInit(): Promise<void> {
    return this.store.load();
  }

  /** Só as colunas abertas recebem cartões. */
  protected readonly acceptsDrop = (_: CdkDrag, list: CdkDropList<OpportunityStage>): boolean =>
    OPEN_STAGES.includes(list.data);

  protected canMove(card: OpportunitySummary): boolean {
    return card.stage !== 'won' && this.access.can('crm.opportunities.manage');
  }

  protected drop(event: CdkDragDrop<OpportunityStage, OpportunityStage, OpportunitySummary>): void {
    void this.store.move(
      event.item.data.id,
      event.previousContainer.data,
      event.container.data,
      event.currentIndex,
    );
  }
}
