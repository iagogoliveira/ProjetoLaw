import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type * as dto from '../../../api/types.gen';
import { toResult } from '../../../core/data/api-result';

@Injectable({ providedIn: 'root' })
export class NotificationSettingsApi {
  settings() {
    return toResult(sdk.getNotificationSettings());
  }
  saveSettings(body: dto.NotificationSettingsInput) {
    return toResult(sdk.setNotificationSettings({ body }));
  }
  rules() {
    return toResult(sdk.getNotificationRules());
  }
  saveRules(body: dto.NotificationRulesInput) {
    return toResult(sdk.setNotificationRules({ body }));
  }
}
