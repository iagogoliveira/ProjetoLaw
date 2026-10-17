import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { InvitationPreviewResponse } from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { OrganizationApi } from '../../../core/organization/data/organization-api';
import { PendingInvitation } from '../../../core/organization/pending-invitation';

interface InvitationState {
  token: string | null;
  preview: InvitationPreviewResponse | null;
  loading: boolean;
  busy: boolean;
  errorCode: string | null;
}

const initialState: InvitationState = {
  token: null,
  preview: null,
  loading: false,
  busy: false,
  errorCode: null,
};

/** Convite aberto pelo link do e-mail: consulta sem login e aceite com a conta do e-mail convidado. */
export const InvitationStore = signalStore(
  withState(initialState),
  withMethods((store, api = inject(OrganizationApi), access = inject(AccessStore)) => ({
    async open(token: string | null): Promise<void> {
      if (!token) {
        patchState(store, { errorCode: 'organization.invitation_not_found' });
        return;
      }

      PendingInvitation.save(token);
      patchState(store, { token, loading: true, errorCode: null });
      const preview = await api.preview(token);
      if (!preview.ok) {
        PendingInvitation.clear();
      }

      patchState(
        store,
        preview.ok
          ? { preview: preview.value, loading: false }
          : { errorCode: preview.code, loading: false },
      );
    },

    /** Aceita e entra no escritório do convite; devolve se deu certo. */
    async accept(): Promise<boolean> {
      const token = store.token();
      if (!token) {
        return false;
      }

      patchState(store, { busy: true, errorCode: null });
      const accepted = await api.accept(token);
      if (!accepted.ok) {
        if (accepted.code !== 'organization.invitation_email_mismatch') {
          PendingInvitation.clear();
        }

        patchState(store, { busy: false, errorCode: accepted.code });
        return false;
      }

      PendingInvitation.clear();
      const switched = await access.switchTo(accepted.value.organizationId);
      patchState(store, { busy: false });
      return switched;
    },
  })),
);
