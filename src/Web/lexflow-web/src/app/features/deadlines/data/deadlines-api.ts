import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type * as dto from '../../../api/types.gen';
import { toResult } from '../../../core/data/api-result';

export interface DeadlineFilters {
  from: string;
  until: string;
  assigneeUserId: string | null;
  teamId: string | null;
  includeClosed: boolean;
  caseId?: string | null;
}

@Injectable({ providedIn: 'root' })
export class DeadlinesApi {
  list(filters: DeadlineFilters, cursor: string | null) {
    return toResult(
      sdk.listDeadlines({
        query: {
          ...filters,
          assigneeUserId: filters.assigneeUserId ?? undefined,
          teamId: filters.teamId ?? undefined,
          caseId: filters.caseId ?? undefined,
          cursor: cursor ?? undefined,
          limit: 50,
        },
      }),
    );
  }
  detail(id: string) {
    return toResult(sdk.getDeadline({ path: { id } }));
  }
  create(caseId: string, body: dto.ManualDeadlineRequest) {
    return toResult(sdk.registerDeadline({ path: { id: caseId }, body }));
  }
  start(id: string) {
    return toResult(sdk.startDeadline({ path: { id } }));
  }
  review(id: string) {
    return toResult(sdk.reviewDeadline({ path: { id } }));
  }
  complete(id: string, evidence: string) {
    return toResult(sdk.completeDeadline({ path: { id }, body: { evidence } }));
  }
  miss(id: string, reason: string) {
    return toResult(sdk.missDeadline({ path: { id }, body: { reason } }));
  }
  calculate(body: dto.CalculateDeadlineQuery) {
    return toResult(sdk.calculateDeadline({ body }));
  }
  options() {
    return toResult(sdk.getCaseOptions());
  }
  cases(q: string) {
    return toResult(sdk.listCases({ query: { Q: q, Status: 'active' } }));
  }
  case(id: string) {
    return toResult(sdk.getCase({ path: { id } }));
  }
  caseDeadlines(id: string) {
    return toResult(sdk.listCaseDeadlines({ path: { id } }));
  }
  preference() {
    return toResult(sdk.getMyPreference({ query: { key: 'deadlines.list' } }));
  }
  savePreference(value: dto.JsonElement) {
    return toResult(sdk.setMyPreference({ body: { key: 'deadlines.list', value } }));
  }
}
