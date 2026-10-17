import { TestBed } from '@angular/core/testing';
import type { MeResponse, SessionResponse } from '../../../api/types.gen';
import { AccountApi, ApiResult } from './account-api';
import { AccountStore } from './account-store';

const profile = (mfaMethod = 'none'): MeResponse => ({
  id: 'u1',
  email: 'ana@exemplo.com.br',
  fullName: 'Ana Souza',
  emailConfirmed: true,
  mfaMethod,
  createdAt: '2026-10-16T12:00:00Z',
  lastLoginAt: null,
});

const session = (id: string, current = false): SessionResponse => ({
  id,
  createdAt: '2026-10-16T12:00:00Z',
  lastSeenAt: '2026-10-16T12:00:00Z',
  expiresAt: '2026-10-30T12:00:00Z',
  ipAddress: '203.0.113.7',
  userAgent: 'Firefox',
  current,
});

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string): Promise<ApiResult<never>> => Promise.resolve({ ok: false, code });

describe('AccountStore', () => {
  function setup(api: Partial<AccountApi>) {
    TestBed.configureTestingModule({
      providers: [
        AccountStore,
        {
          provide: AccountApi,
          useValue: { me: () => ok(profile()), sessions: () => ok([]), ...api },
        },
      ],
    });
    return TestBed.inject(AccountStore);
  }

  it('loads the profile and the open sessions', async () => {
    const store = setup({ sessions: () => ok([session('s1', true), session('s2')]) });

    await store.load();

    expect(store.profile()?.fullName).toBe('Ana Souza');
    expect(store.sessions().map((s) => s.id)).toEqual(['s1', 's2']);
    expect(store.mfaEnabled()).toBe(false);
    expect(store.loading()).toBe(false);
  });

  it('removes an ended session from the list', async () => {
    const store = setup({
      sessions: () => ok([session('s1', true), session('s2')]),
      endSession: () => ok(undefined),
    });
    await store.load();

    await store.endSession('s2');

    expect(store.sessions().map((s) => s.id)).toEqual(['s1']);
  });

  it('turns totp on in two steps and keeps the recovery codes to show once', async () => {
    let method = 'none';
    const store = setup({
      me: () => ok(profile(method)),
      startTotp: () => ok({ sharedKey: 'ABCD', authenticatorUri: 'otpauth://totp/LexFlow' }),
      confirmTotp: () => {
        method = 'totp';
        return ok({ recoveryCodes: ['AAAAA-BBBBB'] });
      },
    });
    await store.load();

    await store.startTotp();
    expect(store.totpSetup()?.sharedKey).toBe('ABCD');
    await store.confirmTotp('123456');

    expect(store.totpSetup()).toBeNull();
    expect(store.recoveryCodes()).toEqual(['AAAAA-BBBBB']);
    expect(store.mfaMethod()).toBe('totp');
  });

  it('keeps the error code of a failed confirmation for translation', async () => {
    const store = setup({
      startTotp: () => ok({ sharedKey: 'ABCD', authenticatorUri: 'otpauth://totp/LexFlow' }),
      confirmTotp: () => fail('identity.mfa_code_invalid'),
    });
    await store.load();
    await store.startTotp();

    await store.confirmTotp('000000');

    expect(store.errorCode()).toBe('identity.mfa_code_invalid');
    expect(store.totpSetup()).not.toBeNull();
    expect(store.busy()).toBe(false);
  });

  it('turns the email code on and reloads the profile', async () => {
    let method = 'none';
    const store = setup({
      me: () => ok(profile(method)),
      enableEmailMfa: () => {
        method = 'email';
        return ok(undefined);
      },
    });
    await store.load();

    await store.enableEmailMfa();

    expect(store.mfaMethod()).toBe('email');
  });

  it('reports a failed load', async () => {
    const store = setup({ me: () => fail('auth.unauthenticated') });

    await store.load();

    expect(store.errorCode()).toBe('auth.unauthenticated');
    expect(store.profile()).toBeNull();
  });
});
