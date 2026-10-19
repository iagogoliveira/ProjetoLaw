import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatStepperModule } from '@angular/material/stepper';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type {
  AssignmentRole,
  CaseInput,
  CaseInstance,
  CaseKind,
  PartyPole,
  PersonKind,
} from '../../../api/types.gen';
import { digitsOf, formatDocument } from '../../../shared/br-format';
import { CaseEditorStore } from '../data/case-stores';

/** Parte como a tela edita: a pessoa (com o nome para mostrar), o polo, se é cliente e o advogado contrário. */
interface PartyRow {
  personId: string;
  name: string;
  pole: PartyPole;
  isClient: boolean;
  counselName: string;
  counselOab: string;
}

interface AssignmentRow {
  role: AssignmentRole;
  userId: string | null;
  teamId: string | null;
  label: string;
}

/** Espera entre a última tecla e a busca de pessoas. */
const SEARCH_DELAY_MS = 300;

/**
 * Assistente de cadastro e edição do processo (5E1): tipo e número CNJ (com o tribunal pela consulta), dados,
 * partes (pessoas do Crm, com cadastro rápido), responsáveis e revisão. Na edição, o tipo não muda.
 */
@Component({
  selector: 'lf-case-wizard-page',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatStepperModule,
    ReactiveFormsModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [CaseEditorStore],
  templateUrl: './case-wizard-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CaseWizardPage implements OnInit, OnDestroy {
  /** Id do processo na rota de edição (`:id/editar`). */
  readonly id = input<string>();

  protected readonly store = inject(CaseEditorStore);
  private readonly router = inject(Router);
  protected readonly kinds: CaseKind[] = ['judicial', 'administrative', 'extrajudicial'];
  protected readonly instances: CaseInstance[] = ['first', 'second', 'superior', 'supreme'];
  protected readonly poles: PartyPole[] = ['active', 'passive', 'third'];
  protected readonly formatDocument = formatDocument;

  protected readonly form = inject(FormBuilder).nonNullable.group({
    kind: 'judicial' as CaseKind,
    cnjNumber: '',
    instance: null as CaseInstance | null,
    practiceAreaId: null as string | null,
    branchId: null as string | null,
    phaseId: null as string | null,
    actionTypeId: null as string | null,
    district: '',
    courtUnit: '',
    rite: '',
    proceduralClass: '',
    subject: '',
    claimValue: '',
    filedOn: '',
    servedOn: '',
    description: '',
  });

  protected readonly parties = signal<PartyRow[]>([]);
  protected readonly assignments = signal<AssignmentRow[]>([]);
  protected readonly personTerm = signal('');
  protected readonly newPerson = signal({ name: '', document: '', kind: 'company' as PersonKind });
  protected readonly member = signal<string | null>(null);
  protected readonly memberRole = signal<AssignmentRole>('lawyer');
  protected readonly team = signal<string | null>(null);
  protected readonly editing = computed(() => this.store.legalCase() !== null);
  protected readonly hasClient = computed(() => this.parties().some((p) => p.isClient));

  private timer: ReturnType<typeof setTimeout> | undefined;

  async ngOnInit(): Promise<void> {
    await this.store.load(this.id() ?? null);
    const existing = this.store.legalCase();
    if (!existing) {
      return;
    }

    this.form.patchValue({
      kind: existing.kind,
      cnjNumber: existing.cnjNumber ?? '',
      instance: existing.instance,
      practiceAreaId: existing.practiceAreaId,
      branchId: existing.branchId,
      phaseId: existing.phaseId,
      actionTypeId: existing.actionTypeId,
      district: existing.district ?? '',
      courtUnit: existing.courtUnit ?? '',
      rite: existing.rite ?? '',
      proceduralClass: existing.proceduralClass ?? '',
      subject: existing.subject ?? '',
      claimValue: existing.claimValue === null ? '' : String(existing.claimValue),
      filedOn: existing.filedOn ?? '',
      servedOn: existing.servedOn ?? '',
      description: existing.description ?? '',
    });
    this.form.controls.kind.disable();
    this.parties.set(
      existing.parties.map((p) => ({
        personId: p.personId,
        name: p.name,
        pole: p.pole,
        isClient: p.isClient,
        counselName: p.counselName ?? '',
        counselOab: p.counselOab ?? '',
      })),
    );
    this.assignments.set(
      existing.assignments.map((a) => ({
        role: a.role,
        userId: a.userId,
        teamId: a.teamId,
        label: this.labelOf(a.userId, a.teamId),
      })),
    );
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  protected async lookupCnj(): Promise<void> {
    const cnj = this.form.controls.cnjNumber.value;
    if (digitsOf(cnj).length === 20) {
      const info = await this.store.lookupCnj(cnj);
      if (info) {
        this.form.controls.cnjNumber.setValue(info.formatted);
      }
    }
  }

  protected onPersonTerm(value: string): void {
    this.personTerm.set(value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.store.searchPeople(value), SEARCH_DELAY_MS);
  }

  protected addParty(personId: string, name: string, isClient: boolean): void {
    if (this.parties().some((p) => p.personId === personId)) {
      return;
    }

    this.parties.update((list) => [
      ...list,
      {
        personId,
        name,
        pole: isClient ? 'active' : 'passive',
        isClient,
        counselName: '',
        counselOab: '',
      },
    ]);
  }

  protected async createPerson(isClient: boolean): Promise<void> {
    const draft = this.newPerson();
    if (!draft.name.trim()) {
      return;
    }

    const person = await this.store.createPerson(
      draft.kind,
      draft.name.trim(),
      digitsOf(draft.document) || null,
      isClient ? 'client' : 'opponent',
    );
    if (person) {
      this.addParty(person.id, person.name, isClient);
      this.newPerson.set({ name: '', document: '', kind: 'company' });
    }
  }

  protected updateParty(index: number, change: Partial<PartyRow>): void {
    this.parties.update((list) => list.map((p, i) => (i === index ? { ...p, ...change } : p)));
  }

  protected removeParty(index: number): void {
    this.parties.update((list) => list.filter((_, i) => i !== index));
  }

  protected addMember(): void {
    const userId = this.member();
    if (
      !userId ||
      this.assignments().some((a) => a.userId === userId && a.role === this.memberRole())
    ) {
      return;
    }

    this.assignments.update((list) => [
      ...list,
      { role: this.memberRole(), userId, teamId: null, label: this.labelOf(userId, null) },
    ]);
    this.member.set(null);
  }

  protected addTeam(): void {
    const teamId = this.team();
    if (!teamId || this.assignments().some((a) => a.teamId === teamId)) {
      return;
    }

    this.assignments.update((list) => [
      ...list,
      { role: 'team', userId: null, teamId, label: this.labelOf(null, teamId) },
    ]);
    this.team.set(null);
  }

  protected removeAssignment(index: number): void {
    this.assignments.update((list) => list.filter((_, i) => i !== index));
  }

  protected nameOf(
    list: 'practiceAreas' | 'branches' | 'phases' | 'actionTypes',
    id: string | null,
  ): string {
    return (id && this.store.options()?.[list].find((o) => o.id === id)?.name) || '—';
  }

  protected fieldError(field: string): string | null {
    return this.store.fieldErrors()?.[field]?.[0]?.code ?? null;
  }

  protected async save(): Promise<void> {
    const v = this.form.getRawValue();
    const text = (value: string) => value.trim() || null;
    const input: CaseInput = {
      cnjNumber: v.kind === 'judicial' ? text(v.cnjNumber) : null,
      courtId: null,
      instance: v.instance,
      district: text(v.district),
      courtUnit: text(v.courtUnit),
      rite: text(v.rite),
      proceduralClass: text(v.proceduralClass),
      subject: text(v.subject),
      actionTypeId: v.actionTypeId,
      practiceAreaId: v.practiceAreaId,
      branchId: v.branchId,
      phaseId: v.phaseId,
      claimValue: v.claimValue.trim()
        ? Number(v.claimValue.replace(/\./g, '').replace(',', '.'))
        : null,
      filedOn: text(v.filedOn),
      servedOn: text(v.servedOn),
      description: text(v.description),
      payerPersonId: null,
      parties: this.parties().map((p) => ({
        personId: p.personId,
        pole: p.pole,
        isClient: p.isClient,
        counselName: p.counselName.trim() || null,
        counselOab: p.counselOab.trim() || null,
      })),
      assignments: this.assignments().map((a) => ({
        role: a.role,
        userId: a.userId,
        teamId: a.teamId,
      })),
      customFields: null,
    };
    const saved = await this.store.save(v.kind, input);
    if (saved) {
      await this.router.navigate(['/processos', saved.id]);
    }
  }

  private labelOf(userId: string | null, teamId: string | null): string {
    const options = this.store.options();
    return (
      (userId && options?.members.find((m) => m.userId === userId)?.name) ||
      (teamId && options?.teams.find((t) => t.id === teamId)?.name) ||
      '—'
    );
  }
}
