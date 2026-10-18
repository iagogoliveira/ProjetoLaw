import { CurrencyPipe, DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormArray,
  FormControl,
  FormGroup,
  FormsModule,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { FeeKind, ProposalInput, ProposalItemInput } from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { CanDirective } from '../../../core/organization/can.directive';
import { ProposalStore } from '../data/proposal-store';

const KINDS: readonly FeeKind[] = ['fixed', 'monthly', 'hourly', 'success', 'perAct'];

type ItemForm = FormGroup<{
  description: FormControl<string>;
  kind: FormControl<FeeKind>;
  quantity: FormControl<number>;
  unitAmount: FormControl<number | null>;
  percentage: FormControl<number | null>;
}>;

/** Ação com observação aberta para confirmação. */
type NoteAction = 'return' | 'decline' | 'delete' | null;

const round2 = (value: number): number => Math.round(value * 100) / 100;

/**
 * Proposta de honorários (RF-08): o rascunho (título, escopo, condições, validade, modelo e itens com a modalidade,
 * com o total calculado sem o êxito) e o fluxo: enviar para aprovação, aprovar (quem aprova, nunca quem elaborou),
 * devolver, registrar o envio (congela o DOCX), o aceite (cria o cliente) ou a recusa; baixar o DOCX.
 */
@Component({
  selector: 'lf-proposal-page',
  imports: [
    CanDirective,
    CurrencyPipe,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [ProposalStore],
  templateUrl: './proposal-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProposalPage implements OnInit {
  /** Oportunidade da proposta nova (`funil/:id/propostas/nova`). */
  readonly id = input<string>();

  /** Proposta existente (`funil/propostas/:proposalId`). */
  readonly proposalId = input<string>();

  protected readonly store = inject(ProposalStore);
  protected readonly kinds = KINDS;
  protected readonly pending = signal<NoteAction>(null);
  protected readonly note = signal('');

  /** Rascunho editável por quem gerencia propostas (ou proposta nova). */
  protected readonly editable = computed(() => {
    const proposal = this.store.proposal();
    return this.access.can('crm.proposals.manage') && (!proposal || proposal.status === 'draft');
  });

  private readonly access = inject(AccessStore);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly changeDetector = inject(ChangeDetectorRef);

  protected readonly form = this.fb.group({
    title: this.fb.control('', [Validators.required, Validators.maxLength(200)]),
    scope: this.fb.control('', Validators.maxLength(8000)),
    conditions: this.fb.control('', Validators.maxLength(8000)),
    validUntil: this.fb.control(''),
    templateId: this.fb.control<string | null>(null),
    items: this.fb.array<ItemForm>([]),
  });

  private readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.value });

  /** Total como o servidor calcula: quantidade × valor, sem o êxito. */
  protected readonly total = computed(() =>
    round2(
      (this.values().items ?? [])
        .filter((i) => i.kind !== 'success')
        .reduce((sum, i) => sum + round2(Number(i.quantity ?? 0) * Number(i.unitAmount ?? 0)), 0),
    ),
  );

  protected get items(): FormArray<ItemForm> {
    return this.form.controls.items;
  }

  async ngOnInit(): Promise<void> {
    await this.store.load(this.proposalId() ?? null, this.id() ?? null);
    const proposal = this.store.proposal();
    if (proposal) {
      this.form.patchValue({
        title: proposal.title,
        scope: proposal.scope ?? '',
        conditions: proposal.conditions ?? '',
        validUntil: proposal.validUntil ?? '',
        templateId: proposal.templateId,
      });
      proposal.items.forEach((i) =>
        this.addItem({
          description: i.description,
          kind: i.kind,
          quantity: Number(i.quantity),
          unitAmount: i.unitAmount === null ? null : Number(i.unitAmount),
          percentage: i.percentage === null ? null : Number(i.percentage),
        }),
      );
    } else {
      this.addItem();
    }
    if (!this.editable()) {
      this.form.disable();
    }
    this.changeDetector.markForCheck();
  }

  protected addItem(value?: ProposalItemInput): void {
    this.items.push(
      this.fb.group({
        description: this.fb.control(value?.description ?? '', [
          Validators.required,
          Validators.maxLength(500),
        ]),
        kind: this.fb.control<FeeKind>(value?.kind ?? 'fixed'),
        quantity: this.fb.control(Number(value?.quantity ?? 1), Validators.min(0.01)),
        unitAmount: this.fb.control<number | null>(
          value?.unitAmount === undefined || value?.unitAmount === null
            ? null
            : Number(value.unitAmount),
          Validators.min(0),
        ),
        percentage: this.fb.control<number | null>(
          value?.percentage === undefined || value?.percentage === null
            ? null
            : Number(value.percentage),
          [Validators.min(0.01), Validators.max(100)],
        ),
      }),
    );
  }

  protected amountOf(index: number): number | null {
    const item = this.values().items?.[index];
    return !item || item.kind === 'success'
      ? null
      : round2(Number(item.quantity ?? 0) * Number(item.unitAmount ?? 0));
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const isNew = !this.store.proposal();
    const saved = await this.store.save(this.toInput(), this.id() ?? null);
    if (saved) {
      this.form.markAsPristine();
      if (isNew) {
        await this.router.navigate(['/funil/propostas', saved.id]);
      }
    }
  }

  protected open(action: NoteAction): void {
    this.note.set('');
    this.pending.set(action);
  }

  protected async confirm(): Promise<void> {
    const action = this.pending();
    if (action === 'delete') {
      const opportunity = this.store.proposal()!.opportunityId;
      if (await this.store.remove()) {
        await this.router.navigate(['/funil', opportunity]);
      }
      return;
    }

    if (action && (await this.store.step(action, this.note()))) {
      this.pending.set(null);
      if (action === 'return') {
        this.form.enable();
      }
    }
  }

  protected async submit(): Promise<void> {
    if (await this.store.step('submit')) {
      this.form.disable();
    }
  }

  private toInput(): ProposalInput {
    const v = this.form.getRawValue();
    return {
      title: v.title.trim(),
      scope: v.scope.trim() || null,
      conditions: v.conditions.trim() || null,
      validUntil: v.validUntil || null,
      templateId: v.templateId,
      items: v.items.map((i) => ({
        description: i.description.trim(),
        kind: i.kind,
        quantity: i.kind === 'success' ? 1 : Number(i.quantity),
        unitAmount: i.kind === 'success' ? null : Number(i.unitAmount ?? 0),
        percentage: i.kind === 'success' ? Number(i.percentage ?? 0) : null,
      })),
    };
  }
}
