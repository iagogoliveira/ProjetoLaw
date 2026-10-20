import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import { toResult } from '../../../core/data/api-result';

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  list(cursor: string | null) {
    return toResult(
      sdk.listNotifications({
        query: { cursor: cursor ?? undefined, unreadOnly: true, limit: 50 },
      }),
    );
  }
  read(id: string) {
    return toResult(sdk.readNotification({ path: { id } }));
  }
}
