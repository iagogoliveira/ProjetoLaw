import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoDirective } from '@jsverse/transloco';
import type {
  OfferParty,
  SettlementDirection,
  SettlementInstallmentResponse,
  SettlementResponse,
} from '../../../../api/types.gen';
import { CanDirective } from '../../../../core/organization/can.directive';
import { SettlementsStore } from '../../data/settlements-store';
import {
  editAmount,
  generateInstallments,
  MONEY_PATTERN,
  parseAmount,
  today,
} from '../../data/values-format';
import { ValueErrors } from './value-errors';

type Action =
  'offer' | 'submit' | 'approve' | 'return' | 'reject' | 'cancel' | 'homologate' | 'pay';

@Component({
  selector: 'lf-settlements-tab',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslocoDirective,
    CanDirective,
    ValueErrors,
  ],
  providers: [SettlementsStore],
  templateUrl: './settlements-tab.html',
  styleUrl: '../../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettlementsTab implements OnInit {
  readonly caseId = input.required<string>();
  readonly closed = input(false);
  readonly changed = output<void>();
  protected readonly store = inject(SettlementsStore);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly creating = signal(false);
  protected readonly pending = signal<{
    id: string;
    action: Action;
    installmentId?: string;
  } | null>(null);
  protected readonly startForm = this.fb.group({
    direction: 'clientPays' as SettlementDirection,
    notes: '',
  });
  protected readonly offerForm = this.fb.group({
    party: 'office' as OfferParty,
    amount: ['', [Validators.required, Validators.pattern(MONEY_PATTERN)]],
    terms: '',
    offeredOn: [today(), Validators.required],
  });
  protected readonly termsForm = this.fb.group({
    amount: ['', [Validators.required, Validators.pattern(MONEY_PATTERN)]],
    count: [1, [Validators.required, Validators.min(1), Validators.max(360)]],
    firstDue: [today(), Validators.required],
    interval: [1, [Validators.required, Validators.min(1), Validators.max(120)]],
  });
  protected readonly transitionForm = this.fb.group({
    reason: '',
    date: [today(), Validators.required],
  });
  protected readonly payForm = this.fb.group({
    paidOn: [today(), Validators.required],
    paidAmount: ['', [Validators.required, Validators.pattern(MONEY_PATTERN)]],
  });
  private readonly termChanges = toSignal(this.termsForm.valueChanges);
  protected readonly installments = computed(() => {
    this.termChanges();
    const v = this.termsForm.getRawValue();
    return generateInstallments(
      parseAmount(v.amount) ?? 0,
      Number(v.count),
      v.firstDue,
      Number(v.interval),
    );
  });
  protected readonly amount = (v: number | string | null) =>
    v === null
      ? '—'
      : new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(Number(v));
  protected readonly date = (v: string | null) => (v ? v.split('-').reverse().join('/') : '—');
  ngOnInit(): void {
    void this.store.load(this.caseId());
  }
  protected open(
    s: SettlementResponse,
    action: Action,
    installment?: SettlementInstallmentResponse,
  ): void {
    this.pending.set({ id: s.id, action, installmentId: installment?.id });
    this.transitionForm.reset({ date: today(), reason: '' });
    this.termsForm.reset({
      amount: editAmount(s.agreedAmount ?? s.offers.at(-1)?.amount ?? null),
      count: 1,
      firstDue: today(),
      interval: 1,
    });
    this.offerForm.reset({ party: 'office', amount: '', terms: '', offeredOn: today() });
    this.payForm.reset({ paidOn: today(), paidAmount: editAmount(installment?.amount ?? null) });
  }
  protected async start(): Promise<void> {
    const v = this.startForm.getRawValue();
    if (await this.store.start({ ...v, notes: v.notes.trim() || null, firstOffer: null })) {
      this.creating.set(false);
      this.startForm.reset();
      this.changed.emit();
    }
  }
  protected async offer(): Promise<void> {
    if (this.offerForm.invalid) return;
    const v = this.offerForm.getRawValue();
    await this.finish(
      await this.store.offer(this.pending()!.id, {
        ...v,
        amount: parseAmount(v.amount)!,
        terms: v.terms.trim() || null,
      }),
    );
  }
  protected async submit(): Promise<void> {
    if (this.termsForm.invalid || !this.installments().length) return;
    await this.finish(
      await this.store.submit(this.pending()!.id, {
        agreedAmount: parseAmount(this.termsForm.controls.amount.value)!,
        installments: this.installments(),
      }),
    );
  }
  protected async transition(): Promise<void> {
    const p = this.pending()!;
    if (p.action === 'offer' || p.action === 'submit' || p.action === 'pay') return;
    const v = this.transitionForm.getRawValue();
    if (p.action === 'homologate' && !v.date) return;
    await this.finish(
      await this.store.step(p.id, p.action, p.action === 'homologate' ? v.date : v.reason.trim()),
    );
  }
  protected async pay(): Promise<void> {
    const p = this.pending()!;
    const v = this.payForm.getRawValue();
    if (this.payForm.invalid || (parseAmount(v.paidAmount) ?? 0) <= 0) return;
    await this.finish(
      await this.store.pay(p.id, p.installmentId!, {
        paidOn: v.paidOn,
        paidAmount: parseAmount(v.paidAmount)!,
      }),
    );
  }
  private async finish(done: boolean): Promise<void> {
    if (done) {
      this.pending.set(null);
      this.changed.emit();
    }
  }
}
