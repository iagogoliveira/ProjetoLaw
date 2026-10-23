import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type * as dto from '../../../api/types.gen';
import { toResult } from '../../../core/data/api-result';

@Injectable({ providedIn: 'root' })
export class CalendarSyncApi {
  connections() {
    return toResult(sdk.listCalendarConnections());
  }
  connect(body: dto.ConnectCalendarCommand) {
    return toResult(sdk.connectCalendar({ body }));
  }
  disconnect(id: string) {
    return toResult(sdk.disconnectCalendar({ path: { id } }));
  }
  queueSync(id: string) {
    return toResult(sdk.queueCalendarSync({ path: { id } }));
  }
  pending(id: string, after: string | null) {
    return toResult(
      sdk.listCalendarPending({ path: { id }, query: { after: after ?? undefined } }),
    );
  }
  resolve(id: string, pendingId: string, resolution: dto.CalendarResolution) {
    return toResult(sdk.resolveCalendarPending({ path: { id, pendingId }, body: { resolution } }));
  }
}
