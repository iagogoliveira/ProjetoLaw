import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { CalendarConnectionResponse, CalendarPendingResponse } from '../../../api/types.gen';
import { CalendarSyncApi } from './calendar-sync-api';

interface State {
  connections: CalendarConnectionResponse[];
  isSimulation: boolean;
  isAvailable: boolean;
  selectedId: string | null;
  pending: CalendarPendingResponse[];
  pendingCursor: string | null;
  loading: boolean;
  pendingLoading: boolean;
  pendingError: string | null;
  busy: boolean;
  error: string | null;
  queued: boolean;
  changedAgain: boolean;
}
const initial: State = {
  connections: [],
  isSimulation: false,
  isAvailable: false,
  selectedId: null,
  pending: [],
  pendingCursor: null,
  loading: false,
  pendingLoading: false,
  pendingError: null,
  busy: false,
  error: null,
  queued: false,
  changedAgain: false,
};

export const CalendarSyncStore = signalStore(
  withState<State>(initial),
  withMethods((store, api = inject(CalendarSyncApi)) => {
    let connectionVersion = 0;
    let pendingVersion = 0;
    let operationVersion = 0;
    function cancelCurrentOperation(): void {
      operationVersion++;
      patchState(store, { busy: false });
    }
    async function loadConnections(): Promise<void> {
      const version = ++connectionVersion;
      const previousSelection = store.selectedId();
      patchState(store, { loading: true, error: null });
      const result = await api.connections();
      if (version !== connectionVersion) return;
      if (!result.ok) {
        cancelCurrentOperation();
        patchState(store, { ...initial, error: result.code });
        return;
      }
      const selectedId = result.value.items.some((item) => item.id === previousSelection)
        ? previousSelection
        : null;
      patchState(store, {
        connections: result.value.items,
        isSimulation: result.value.isSimulation,
        isAvailable: result.value.isAvailable,
        selectedId,
        loading: false,
        error: null,
      });
      if (selectedId !== previousSelection) void select(selectedId);
    }
    function clearPending(): void {
      pendingVersion++;
      patchState(store, {
        pending: [],
        pendingCursor: null,
        pendingError: null,
        pendingLoading: false,
      });
    }
    async function select(id: string | null): Promise<void> {
      cancelCurrentOperation();
      pendingVersion++;
      patchState(store, {
        selectedId: id,
        pending: [],
        pendingCursor: null,
        pendingError: null,
        pendingLoading: false,
        changedAgain: false,
      });
      if (id) await loadPending(id, null, false);
    }
    async function loadPending(
      id = store.selectedId(),
      cursor: string | null = null,
      append = false,
    ): Promise<void> {
      if (!id || store.pendingLoading()) return;
      const version = ++pendingVersion;
      patchState(store, { pendingLoading: true, pendingError: null });
      const result = await api.pending(id, cursor);
      if (version !== pendingVersion || store.selectedId() !== id) return;
      if (!result.ok) {
        const inaccessible = isAccessFailure(result.code);
        if (inaccessible) {
          cancelCurrentOperation();
          patchState(store, { selectedId: null });
          clearPending();
        } else patchState(store, { pendingLoading: false, pendingError: result.code });
        return;
      }
      patchState(store, {
        pending: append ? [...store.pending(), ...result.value.items] : result.value.items,
        pendingCursor: result.value.nextCursor,
        pendingLoading: false,
      });
    }
    async function run(
      action: () =>
        | ReturnType<CalendarSyncApi['connect']>
        | ReturnType<CalendarSyncApi['disconnect']>
        | ReturnType<CalendarSyncApi['queueSync']>,
      queued = false,
      expectedSelection?: string | null,
    ): Promise<boolean> {
      if (store.busy()) return false;
      const operation = ++operationVersion;
      patchState(store, { busy: true, error: null, queued: false });
      const result = await action();
      if (operation !== operationVersion) return false;
      if (expectedSelection !== undefined && expectedSelection !== store.selectedId()) {
        cancelCurrentOperation();
        return false;
      }
      if (!result.ok) {
        const inaccessible = isAccessFailure(result.code);
        if (inaccessible) cancelCurrentOperation();
        patchState(
          store,
          inaccessible ? { ...initial, error: result.code } : { busy: false, error: result.code },
        );
        if (inaccessible) clearPending();
        return false;
      }
      if (expectedSelection === undefined) {
        clearPending();
        patchState(store, { selectedId: result.value.id });
      }
      patchState(store, { busy: false, queued });
      await loadConnections();
      const selected = store.selectedId();
      if (selected) await loadPending(selected, null, false);
      return true;
    }
    async function connect(body: Parameters<CalendarSyncApi['connect']>[0]): Promise<boolean> {
      return run(() => api.connect(body));
    }
    async function disconnect(id: string): Promise<boolean> {
      return run(() => api.disconnect(id), false, id);
    }
    async function queueSync(id: string): Promise<boolean> {
      return run(() => api.queueSync(id), true, id);
    }
    async function resolve(
      id: string,
      pendingId: string,
      resolution: Parameters<CalendarSyncApi['resolve']>[2],
    ): Promise<boolean | null> {
      if (store.busy()) return null;
      const operation = ++operationVersion;
      patchState(store, { busy: true, error: null });
      const result = await api.resolve(id, pendingId, resolution);
      if (operation !== operationVersion) return null;
      if (store.selectedId() !== id) {
        cancelCurrentOperation();
        return null;
      }
      if (!result.ok) {
        const inaccessible = isAccessFailure(result.code);
        if (inaccessible) cancelCurrentOperation();
        patchState(
          store,
          inaccessible ? { ...initial, error: result.code } : { busy: false, error: result.code },
        );
        if (inaccessible) clearPending();
        return false;
      }
      patchState(store, { busy: false, changedAgain: !result.value.resolved });
      await loadPending(id, null, false);
      return result.value.resolved;
    }
    return {
      loadConnections,
      select,
      loadPending,
      loadMorePending: () => loadPending(store.selectedId(), store.pendingCursor(), true),
      connect,
      disconnect,
      queueSync,
      resolve,
    };
  }),
);

function isAccessFailure(code: string): boolean {
  return (
    ['authorization.forbidden', 'auth.forbidden', 'organization.not_a_member'].includes(code) ||
    /not_found$/.test(code)
  );
}
