import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type * as dto from '../../../api/types.gen';
import { toResult } from '../../../core/data/api-result';

@Injectable({ providedIn: 'root' })
export class ControlApi {
  report(query: NonNullable<dto.GetControlReportData['query']>) {
    return toResult(sdk.getControlReport({ query }));
  }
  preference() {
    return toResult(sdk.getMyPreference({ query: { key: 'control.filters' } }));
  }
  savePreference(value: dto.JsonElement) {
    return toResult(sdk.setMyPreference({ body: { key: 'control.filters', value } }));
  }
}
