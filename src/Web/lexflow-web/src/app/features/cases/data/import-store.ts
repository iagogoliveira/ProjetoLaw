import { inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import type { CaseImportResponse } from '../../../api/types.gen';
import { CasesApi } from './cases-api';

export const ImportStore = signalStore(
  withState({
    data: null as CaseImportResponse | null,
    importId: null as string | null,
    busy: false,
    errorCode: null as string | null,
  }),
  withMethods((store, api = inject(CasesApi)) => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    let generation = 0;
    let disposed = false;

    function stop(): void {
      disposed = true;
      generation++;
      clearTimeout(timer);
    }

    async function refresh(): Promise<void> {
      const id = store.importId();
      if (!id || disposed) return;
      clearTimeout(timer);
      const current = ++generation;
      const result = await api.importStatus(id);
      if (disposed || current !== generation) return;
      patchState(
        store,
        result.ok
          ? { data: result.value, errorCode: result.value.errorCode }
          : { errorCode: result.code },
      );
      if (result.ok && ['validating', 'importing'].includes(result.value.status)) {
        timer = setTimeout(() => void refresh(), 1500);
      }
    }

    async function resume(id: string): Promise<void> {
      clearTimeout(timer);
      generation++;
      patchState(store, { importId: id, data: null, errorCode: null });
      await refresh();
    }

    async function upload(file: File): Promise<boolean> {
      if (store.busy()) return false;
      patchState(store, { busy: true, errorCode: null });
      const result = await api.startImport(file);
      if (disposed) return false;
      if (result.ok) await resume(result.value.importId);
      patchState(store, { busy: false, ...(!result.ok ? { errorCode: result.code } : {}) });
      return result.ok;
    }

    async function confirm(): Promise<boolean> {
      const id = store.importId();
      if (
        !id ||
        store.busy() ||
        store.data()?.status !== 'ready' ||
        Number(store.data()?.validRows) < 1
      )
        return false;
      patchState(store, { busy: true, errorCode: null });
      const result = await api.confirmImport(id);
      if (disposed) return false;
      if (result.ok) {
        patchState(store, { data: { ...store.data()!, status: 'importing' } });
        await refresh();
      }
      patchState(store, { busy: false, ...(!result.ok ? { errorCode: result.code } : {}) });
      return result.ok;
    }

    async function download(kind: 'template' | 'errors'): Promise<Blob | null> {
      const id = store.importId();
      if (store.busy() || (kind === 'errors' && !id)) return null;
      patchState(store, { busy: true, errorCode: null });
      const result = kind === 'template' ? await api.importTemplate() : await api.importErrors(id!);
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      return result.ok ? result.value : null;
    }

    return { resume, upload, confirm, refresh, download, stop };
  }),
  withHooks({ onDestroy: (store) => store.stop() }),
);
