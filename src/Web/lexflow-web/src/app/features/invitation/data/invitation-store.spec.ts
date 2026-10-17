import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { ApiResult } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import { OrganizationApi } from '../../../core/organization/data/organization-api';
import { PendingInvitation } from '../../../core/organization/pending-invitation';
import { InvitationStore } from './invitation-store';

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string): Promise<ApiResult<never>> => Promise.resolve({ ok: false, code });

const preview = {
  organizationName: 'Souza Advogados',
  email: 'bia@exemplo.com.br',
  expiresAt: '2026-10-24T12:00:00Z',
};

describe('InvitationStore', () => {
  function setup(api: Partial<OrganizationApi>, switchTo = () => Promise.resolve(true)) {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        InvitationStore,
        { provide: OrganizationApi, useValue: { preview: () => ok(preview), ...api } },
        { provide: AccessStore, useValue: { switchTo } },
      ],
    });
    return TestBed.inject(InvitationStore);
  }

  it('shows a valid invitation and keeps the token while the person signs in', async () => {
    const store = setup({});

    await store.open('token-1');

    expect(store.preview()?.organizationName).toBe('Souza Advogados');
    expect(PendingInvitation.peek()).toBe('token-1');
  });

  it('forgets an invalid link and reports it', async () => {
    const store = setup({ preview: () => fail('organization.invitation_not_found') });

    await store.open('token-1');

    expect(store.errorCode()).toBe('organization.invitation_not_found');
    expect(PendingInvitation.peek()).toBeNull();

    await store.open(null);
    expect(store.errorCode()).toBe('organization.invitation_not_found');
  });

  it('accepts and enters the office of the invitation', async () => {
    const switched: string[] = [];
    const store = setup(
      { accept: () => ok({ organizationId: 'org-a', organizationName: 'Souza Advogados' }) },
      (id?: string) => {
        switched.push(id ?? '');
        return Promise.resolve(true);
      },
    );
    await store.open('token-1');

    expect(await store.accept()).toBe(true);

    expect(switched).toEqual(['org-a']);
    expect(PendingInvitation.peek()).toBeNull();
  });

  it('keeps the token when the signed in account is not the invited one', async () => {
    const store = setup({ accept: () => fail('organization.invitation_email_mismatch') });
    await store.open('token-1');

    expect(await store.accept()).toBe(false);

    expect(store.errorCode()).toBe('organization.invitation_email_mismatch');
    expect(PendingInvitation.peek()).toBe('token-1');
  });
});
