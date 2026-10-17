import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  BranchRequest,
  BranchResponse,
  CostCenterRequest,
  CostCenterResponse,
  MemberResponse,
  PartnerRequest,
  PartnerResponse,
  PracticeAreaResponse,
  TeamRequest,
  TeamResponse,
} from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import { SettingsApi, StructureKind } from './settings-api';

interface StructureState {
  branches: BranchResponse[];
  practiceAreas: PracticeAreaResponse[];
  teams: TeamResponse[];
  costCenters: CostCenterResponse[];
  partners: PartnerResponse[];
  /** Para escolher membros de equipe e o sócio que usa o sistema (precisa de `settings.users.read`). */
  members: MemberResponse[];
  busy: boolean;
  errorCode: string | null;
}

const initialState: StructureState = {
  branches: [],
  practiceAreas: [],
  teams: [],
  costCenters: [],
  partners: [],
  members: [],
  busy: false,
  errorCode: null,
};

/** Estrutura do escritório (seção 3.1): filiais, áreas, equipes e seus membros, centros de custo e sócios. */
export const StructureStore = signalStore(
  withState(initialState),
  withMethods((store, api = inject(SettingsApi), access = inject(AccessStore)) => {
    async function load(): Promise<void> {
      const [branches, practiceAreas, teams, costCenters, partners] = await Promise.all([
        api.branches(),
        api.practiceAreas(),
        api.teams(),
        api.costCenters(),
        api.partners(),
      ]);
      const failed = [branches, practiceAreas, teams, costCenters, partners].find((r) => !r.ok);
      if (failed && !failed.ok) {
        patchState(store, { errorCode: failed.code });
        return;
      }

      patchState(store, {
        branches: branches.ok ? branches.value : [],
        practiceAreas: practiceAreas.ok ? practiceAreas.value : [],
        teams: teams.ok ? teams.value : [],
        costCenters: costCenters.ok ? costCenters.value : [],
        partners: partners.ok ? partners.value : [],
      });

      if (access.can('settings.users.read')) {
        const members = await api.members();
        patchState(store, { members: members.ok ? members.value : [] });
      }
    }

    async function change(call: () => Promise<ApiResult<unknown>>): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await call();
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      if (result.ok) {
        await load();
      }

      return result.ok;
    }

    return {
      load,
      saveBranch: (id: string | null, body: BranchRequest) =>
        change(() => api.saveBranch(id, body)),
      savePracticeArea: (id: string | null, name: string) =>
        change(() => api.savePracticeArea(id, { name })),
      /** Equipe e membros num passo só: os membros vão para o id devolvido pela API (criação ou edição). */
      saveTeam: (id: string | null, body: TeamRequest, memberIds: string[]) =>
        change(async () => {
          const saved = await api.saveTeam(id, body);
          return saved.ok ? api.setTeamMembers(saved.value.id, memberIds) : saved;
        }),
      saveCostCenter: (id: string | null, body: CostCenterRequest) =>
        change(() => api.saveCostCenter(id, body)),
      savePartner: (id: string | null, body: PartnerRequest) =>
        change(() => api.savePartner(id, body)),
      remove: (kind: StructureKind, id: string) => change(() => api.deleteStructure(kind, id)),
      memberName: (id: string | null): string => {
        const member = store.members().find((m) => m.id === id);
        return member?.fullName ?? member?.email ?? '—';
      },
    };
  }),
);
