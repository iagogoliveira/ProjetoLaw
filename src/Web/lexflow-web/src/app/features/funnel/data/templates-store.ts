import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { ProposalTemplateResponse } from '../../../api/types.gen';
import { FunnelApi } from './funnel-api';
import { saveFile } from './save-file';

interface TemplatesState {
  templates: ProposalTemplateResponse[];
  /** Marcadores aceitos nos modelos (`{{cliente.nome}}` etc.). */
  markers: string[];
  busy: boolean;
  errorCode: string | null;
  uploaded: boolean;
}

/** Modelos DOCX de proposta do escritório: lista, marcadores, upload, exclusão e o modelo padrão para baixar. */
export const TemplatesStore = signalStore(
  withState<TemplatesState>({
    templates: [],
    markers: [],
    busy: false,
    errorCode: null,
    uploaded: false,
  }),
  withMethods((store, api = inject(FunnelApi)) => {
    async function load(): Promise<void> {
      const result = await api.templates();
      patchState(
        store,
        result.ok
          ? { templates: result.value.templates, markers: result.value.markers }
          : { errorCode: result.code },
      );
    }

    async function upload(name: string, file: File): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null, uploaded: false });
      const result = await api.uploadTemplate(name, file);
      patchState(store, {
        busy: false,
        errorCode: result.ok ? null : result.code,
        uploaded: result.ok,
      });
      if (result.ok) {
        await load();
      }
      return result.ok;
    }

    async function remove(id: string): Promise<void> {
      patchState(store, { busy: true, errorCode: null });
      const result = await api.deleteTemplate(id);
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      if (result.ok) {
        await load();
      }
    }

    async function downloadDefault(): Promise<void> {
      const result = await api.defaultTemplate();
      if (result.ok) {
        saveFile(result.value, 'modelo-proposta-lexflow.docx');
      } else {
        patchState(store, { errorCode: result.code });
      }
    }

    return { load, upload, remove, downloadDefault };
  }),
);
