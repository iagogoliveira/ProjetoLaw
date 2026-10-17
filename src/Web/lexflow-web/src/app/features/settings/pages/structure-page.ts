import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { TranslocoDirective } from '@jsverse/transloco';
import type {
  BranchResponse,
  CostCenterResponse,
  PartnerResponse,
  PracticeAreaResponse,
  TeamResponse,
} from '../../../api/types.gen';
import { StructureStore } from '../data/structure-store';

const name = () =>
  new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(200)],
  });
const optional = () => new FormControl<string | null>(null);

/**
 * Estrutura do escritório: uma aba por cadastro, cada uma com o formulário (criar ou editar) e a lista. A exclusão
 * é barrada pelo servidor enquanto o item estiver em uso (equipes, escopos de perfis, convites).
 */
@Component({
  selector: 'lf-structure-page',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    ReactiveFormsModule,
    TranslocoDirective,
  ],
  providers: [StructureStore],
  templateUrl: './structure-page.html',
  styleUrl: './settings-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StructurePage implements OnInit {
  protected readonly store = inject(StructureStore);

  /** Item em edição em cada aba (nulo = criando um novo). */
  protected readonly editing = signal<Record<string, string | null>>({});

  protected readonly branch = new FormGroup({
    name: name(),
    cnpj: optional(),
    municipalRegistration: optional(),
  });
  protected readonly area = new FormGroup({ name: name() });
  protected readonly team = new FormGroup({
    name: name(),
    practiceAreaId: optional(),
    branchId: optional(),
    memberIds: new FormControl<string[]>([], { nonNullable: true }),
  });
  protected readonly costCenter = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(30)],
    }),
    name: name(),
  });
  protected readonly partner = new FormGroup({
    name: name(),
    sharePercentage: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0.0001),
      Validators.max(100),
    ]),
    memberId: optional(),
  });

  ngOnInit(): Promise<void> {
    return this.store.load();
  }

  protected editingId(kind: string): string | null {
    return this.editing()[kind] ?? null;
  }

  private setEditing(kind: string, id: string | null): void {
    this.editing.update((current) => ({ ...current, [kind]: id }));
  }

  protected editBranch(item: BranchResponse): void {
    this.branch.reset({
      name: item.name,
      cnpj: item.cnpj,
      municipalRegistration: item.municipalRegistration,
    });
    this.setEditing('branches', item.id);
  }

  protected editArea(item: PracticeAreaResponse): void {
    this.area.reset({ name: item.name });
    this.setEditing('practiceAreas', item.id);
  }

  protected editTeam(item: TeamResponse): void {
    this.team.reset({
      name: item.name,
      practiceAreaId: item.practiceAreaId,
      branchId: item.branchId,
      memberIds: item.memberIds,
    });
    this.setEditing('teams', item.id);
  }

  protected editCostCenter(item: CostCenterResponse): void {
    this.costCenter.reset({ code: item.code, name: item.name });
    this.setEditing('costCenters', item.id);
  }

  protected editPartner(item: PartnerResponse): void {
    this.partner.reset({
      name: item.name,
      sharePercentage: Number(item.sharePercentage),
      memberId: item.memberId,
    });
    this.setEditing('partners', item.id);
  }

  protected cancel(kind: string, form: FormGroup): void {
    form.reset();
    this.setEditing(kind, null);
  }

  /** Salva e, se deu certo, limpa o formulário da aba. */
  private async finish(kind: string, form: FormGroup, saved: Promise<boolean>): Promise<void> {
    if (await saved) {
      this.cancel(kind, form);
    }
  }

  protected saveBranch(): Promise<void> | void {
    if (this.branch.invalid) {
      return this.branch.markAllAsTouched();
    }
    const v = this.branch.getRawValue();
    return this.finish(
      'branches',
      this.branch,
      this.store.saveBranch(this.editingId('branches'), {
        name: v.name.trim(),
        cnpj: v.cnpj?.trim() || null,
        municipalRegistration: v.municipalRegistration?.trim() || null,
      }),
    );
  }

  protected saveArea(): Promise<void> | void {
    if (this.area.invalid) {
      return this.area.markAllAsTouched();
    }
    return this.finish(
      'practiceAreas',
      this.area,
      this.store.savePracticeArea(
        this.editingId('practiceAreas'),
        this.area.getRawValue().name.trim(),
      ),
    );
  }

  protected saveTeam(): Promise<void> | void {
    if (this.team.invalid) {
      return this.team.markAllAsTouched();
    }
    const v = this.team.getRawValue();
    return this.finish(
      'teams',
      this.team,
      this.store.saveTeam(
        this.editingId('teams'),
        { name: v.name.trim(), practiceAreaId: v.practiceAreaId, branchId: v.branchId },
        v.memberIds,
      ),
    );
  }

  protected saveCostCenter(): Promise<void> | void {
    if (this.costCenter.invalid) {
      return this.costCenter.markAllAsTouched();
    }
    const v = this.costCenter.getRawValue();
    return this.finish(
      'costCenters',
      this.costCenter,
      this.store.saveCostCenter(this.editingId('costCenters'), {
        code: v.code.trim(),
        name: v.name.trim(),
      }),
    );
  }

  protected savePartner(): Promise<void> | void {
    if (this.partner.invalid) {
      return this.partner.markAllAsTouched();
    }
    const v = this.partner.getRawValue();
    return this.finish(
      'partners',
      this.partner,
      this.store.savePartner(this.editingId('partners'), {
        name: v.name.trim(),
        sharePercentage: v.sharePercentage ?? 0,
        memberId: v.memberId,
      }),
    );
  }

  protected areaName(id: string | null): string {
    return this.store.practiceAreas().find((a) => a.id === id)?.name ?? '—';
  }

  protected branchName(id: string | null): string {
    return this.store.branches().find((b) => b.id === id)?.name ?? '—';
  }
}
