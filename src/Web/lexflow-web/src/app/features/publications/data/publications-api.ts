import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type * as dto from '../../../api/types.gen';
import { toResult } from '../../../core/data/api-result';

export interface PublicationFilters {
  status: dto.PublicationStatus | null;
  linked: boolean | null;
  assignee: string | null;
}
export const PENDING_FILTERS: PublicationFilters = {
  status: 'pending',
  linked: null,
  assignee: null,
};

@Injectable({ providedIn: 'root' })
export class PublicationsApi {
  list(filters: PublicationFilters, cursor: string | null) {
    return toResult(
      sdk.listPublications({
        query: {
          status: filters.status ?? undefined,
          linked: filters.linked ?? undefined,
          assignee: filters.assignee ?? undefined,
          cursor: cursor ?? undefined,
          limit: 50,
        },
      }),
    );
  }
  detail(id: string) {
    return toResult(sdk.getPublication({ path: { id } }));
  }
  options(q = '') {
    return toResult(sdk.getPublicationScreenOptions({ query: { q } }));
  }
  pendingCount() {
    return toResult(sdk.getPublicationPendingCount());
  }
  link(id: string, caseId: string) {
    return toResult(sdk.linkPublication({ path: { id }, body: { caseId } }));
  }
  assign(id: string, assigneeUserId: string | null) {
    return toResult(sdk.assignPublication({ path: { id }, body: { assigneeUserId } }));
  }
  triage(id: string, body: dto.PublicationTriageInput) {
    return toResult(sdk.triagePublication({ path: { id }, body }));
  }
  closing(day: string, beforeRevision: number | null = null) {
    return toResult(
      sdk.getPublicationsDailyClose({
        query: { day, beforeRevision: beforeRevision ?? undefined, limit: 20 },
      }),
    );
  }
  close(day: string) {
    return toResult(sdk.closePublicationsDay({ body: { day } }));
  }
  sources() {
    return toResult(sdk.listCaptureSources());
  }
  saveSource(id: string | null, body: dto.CaptureSourceInput) {
    return toResult(
      id ? sdk.updateCaptureSource({ path: { id }, body }) : sdk.createCaptureSource({ body }),
    );
  }
  capture(id: string, day?: string) {
    return toResult(sdk.getCaptureState({ path: { id }, query: { day } }));
  }
  retryCapture(id: string, day: string) {
    return toResult(sdk.requestPublicationCapture({ path: { id }, body: { day } }));
  }
  alerts(day?: string) {
    return toResult(sdk.listCaptureAlerts({ query: { day, limit: 200 } }));
  }
  integration() {
    return toResult(sdk.getMovementIntegration());
  }
  configure(body: dto.MovementIntegrationInput) {
    return toResult(sdk.configureMovementIntegration({ body }));
  }
  movements(id: string, cursor: string | null) {
    return toResult(
      sdk.listCaseMovements({ path: { id }, query: { cursor: cursor ?? undefined, limit: 50 } }),
    );
  }
  createMovement(id: string, body: dto.MovementInput) {
    return toResult(sdk.createCaseMovement({ path: { id }, body }));
  }
  sync(id: string) {
    return toResult(sdk.getMovementSync({ path: { id } }));
  }
}
