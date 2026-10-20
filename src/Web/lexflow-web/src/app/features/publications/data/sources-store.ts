import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  CaptureSourceInput,
  CaptureSourceResponse,
  CaptureStateResponse,
  CaptureRetryResponse,
  MovementIntegrationInput,
  MovementIntegrationResponse,
} from '../../../api/types.gen';
import { PublicationsApi } from './publications-api';

export const SourcesStore = signalStore(
  withState({
    items: [] as CaptureSourceResponse[],
    integration: null as MovementIntegrationResponse | null,
    capture: null as CaptureStateResponse | null,
    requested: null as CaptureRetryResponse | null,
    loading: false,
    busy: false,
    errorCode: null as string | null,
  }),
  withMethods((store, api = inject(PublicationsApi)) => {
    let captureVersion = 0;
    async function load(): Promise<void> {
      patchState(store, { loading: true, errorCode: null });
      const [sources, integration] = await Promise.all([api.sources(), api.integration()]);
      patchState(store, {
        loading: false,
        items: sources.ok ? sources.value : [],
        integration: integration.ok ? integration.value : null,
        errorCode: !sources.ok ? sources.code : !integration.ok ? integration.code : null,
      });
    }
    return {
      load,
      async inspect(id: string, day?: string): Promise<void> {
        const request = ++captureVersion;
        patchState(store, { capture: null, errorCode: null });
        const result = await api.capture(id, day);
        if (request !== captureVersion) return;
        patchState(store, result.ok ? { capture: result.value } : { errorCode: result.code });
      },
      async retry(id: string, day: string): Promise<boolean> {
        if (store.busy()) return false;
        patchState(store, { busy: true, errorCode: null, requested: null });
        const result = await api.retryCapture(id, day);
        patchState(
          store,
          result.ok
            ? { busy: false, requested: result.value }
            : { busy: false, errorCode: result.code },
        );
        return result.ok;
      },
      async save(id: string | null, input: CaptureSourceInput): Promise<boolean> {
        if (store.busy()) return false;
        patchState(store, { busy: true, errorCode: null });
        const result = await api.saveSource(id, input);
        patchState(store, { busy: false });
        if (!result.ok) {
          patchState(store, { errorCode: result.code });
          return false;
        }
        ++captureVersion;
        patchState(store, { capture: null });
        await load();
        return true;
      },
      async configure(input: MovementIntegrationInput): Promise<boolean> {
        if (store.busy()) return false;
        patchState(store, { busy: true, errorCode: null });
        const result = await api.configure(input);
        patchState(
          store,
          result.ok
            ? { integration: result.value, busy: false }
            : { errorCode: result.code, busy: false },
        );
        return result.ok;
      },
    };
  }),
);
