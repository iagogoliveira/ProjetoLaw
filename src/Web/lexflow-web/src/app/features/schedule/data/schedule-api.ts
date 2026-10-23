import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type * as dto from '../../../api/types.gen';
import { toResult } from '../../../core/data/api-result';

export interface AgendaFilters {
  from: string;
  until: string;
  assigneeUserId: string | null;
  teamId: string | null;
  includeClosed: boolean;
}

@Injectable({ providedIn: 'root' })
export class ScheduleApi {
  agenda(filters: AgendaFilters, cursor: string | null) {
    return toResult(
      sdk.listAgenda({
        query: {
          ...filters,
          assigneeUserId: filters.assigneeUserId ?? undefined,
          teamId: filters.teamId ?? undefined,
          cursor: cursor ?? undefined,
          limit: 100,
        },
      }),
    );
  }
  options() {
    return toResult(sdk.getSchedulingOptions());
  }
  event(id: string) {
    return toResult(sdk.getEvent({ path: { id } }));
  }
  create(body: dto.CalendarEventInput) {
    return toResult(sdk.registerEvent({ body }));
  }
  edit(id: string, body: dto.CalendarEventInput) {
    return toResult(sdk.editEvent({ path: { id }, body }));
  }
  cancel(id: string) {
    return toResult(sdk.cancelEvent({ path: { id } }));
  }
  cases(q: string) {
    return toResult(sdk.listCases({ query: { Q: q, Status: 'active' } }));
  }
  case(id: string) {
    return toResult(sdk.getCase({ path: { id } }));
  }
  preference() {
    return toResult(sdk.getMyPreference({ query: { key: 'agenda.filters' } }));
  }
  savePreference(value: dto.JsonElement) {
    return toResult(sdk.setMyPreference({ body: { key: 'agenda.filters', value } }));
  }
  redistribute(kind: dto.AgendaKind, body: dto.RedistributionInput) {
    if (kind === 'deadline') return toResult(sdk.redistributeDeadlines({ body }));
    if (kind === 'task') return toResult(sdk.redistributeTasks({ body }));
    return toResult(sdk.redistributeEvents({ body }));
  }
}
