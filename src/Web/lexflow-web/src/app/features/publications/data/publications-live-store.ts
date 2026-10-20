import { inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import type { NotificationResponse } from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { NotificationStream } from './notification-stream';
import { NotificationsApi } from './notifications-api';
import { PublicationsApi } from './publications-api';

const initialState = {
  items: [] as NotificationResponse[],
  nextCursor: null as string | null,
  pendingCount: null as number | null,
  revision: 0,
  online: false,
  loading: false,
  errorCode: null as string | null,
};

export const PublicationsLiveStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods(
    (
      store,
      api = inject(NotificationsApi),
      publications = inject(PublicationsApi),
      stream = inject(NotificationStream),
      access = inject(AccessStore),
    ) => {
      let started = false;
      let generation = 0;
      let refreshVersion = 0;
      let countVersion = 0;
      let pageVersion = 0;
      let dispose: (() => void) | undefined;
      let timer: ReturnType<typeof setInterval> | undefined;
      async function refreshCount(): Promise<void> {
        if (!access.can('publications.read')) {
          patchState(store, { pendingCount: null });
          return;
        }
        const request = generation;
        const countRequest = ++countVersion;
        const result = await publications.pendingCount();
        if (request !== generation || countRequest !== countVersion) return;
        patchState(store, { pendingCount: result.ok ? Number(result.value.count) : null });
      }
      async function refresh(): Promise<void> {
        if (!access.can('identity.profile.read')) {
          patchState(store, { items: [], nextCursor: null, errorCode: null });
          await refreshCount();
          return;
        }
        const request = generation;
        const refreshRequest = ++refreshVersion;
        const result = await api.list(null);
        if (request !== generation || refreshRequest !== refreshVersion) return;
        ++pageVersion;
        patchState(
          store,
          result.ok
            ? {
                items: result.value.items,
                nextCursor: result.value.nextCursor,
                errorCode: null,
                loading: false,
              }
            : { items: [], nextCursor: null, errorCode: result.code, loading: false },
        );
        await refreshCount();
      }
      const focus = () => {
        if (started) {
          patchState(store, { revision: store.revision() + 1 });
          void refresh();
        }
      };
      function stop(): void {
        started = false;
        ++generation;
        clearInterval(timer);
        dispose?.();
        dispose = undefined;
        window.removeEventListener('focus', focus);
        patchState(store, initialState);
      }
      return {
        stop,
        refresh,
        refreshCount,
        start(): void {
          if (started) return;
          started = true;
          const request = ++generation;
          void refresh();
          if (access.can('identity.profile.read'))
            dispose = stream.watch(
              (page) => {
                if (request !== generation) return;
                ++refreshVersion;
                ++pageVersion;
                patchState(store, {
                  items: page.items,
                  nextCursor: page.nextCursor,
                  revision: store.revision() + 1,
                  errorCode: null,
                  loading: false,
                });
                void refreshCount();
              },
              (online) => {
                if (request === generation) patchState(store, { online });
              },
            );
          timer = setInterval(focus, 30000);
          window.addEventListener('focus', focus);
        },
        async more(): Promise<void> {
          const cursor = store.nextCursor();
          if (!cursor || store.loading()) return;
          const request = generation;
          const pageRequest = pageVersion;
          patchState(store, { loading: true });
          const result = await api.list(cursor);
          if (request !== generation || pageRequest !== pageVersion) return;
          const ids = new Set(store.items().map((n) => n.id));
          patchState(
            store,
            result.ok
              ? {
                  items: [...store.items(), ...result.value.items.filter((n) => !ids.has(n.id))],
                  nextCursor: result.value.nextCursor,
                  loading: false,
                }
              : { loading: false, errorCode: result.code },
          );
        },
        async read(id: string): Promise<boolean> {
          const request = generation;
          const result = await api.read(id);
          if (request !== generation) return false;
          if (!result.ok) {
            patchState(store, { errorCode: result.code });
            return false;
          }
          patchState(store, { items: store.items().filter((n) => n.id !== id) });
          await refresh();
          return true;
        },
      };
    },
  ),
  withHooks({ onDestroy: (store) => store.stop() }),
);
