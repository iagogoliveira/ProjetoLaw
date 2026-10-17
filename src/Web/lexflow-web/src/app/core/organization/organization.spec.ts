import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import type { MyAccessResponse } from '../../api/types.gen';
import { APP_CONFIG } from '../config/app-config';
import type { ApiResult } from '../data/api-result';
import { AccessStore } from './access-store';
import {
  ActiveOrganization,
  ORGANIZATION_HEADER,
  organizationInterceptor,
} from './active-organization';
import { CanDirective } from './can.directive';
import { OrganizationApi } from './data/organization-api';
import { organizationGuard, permissionGuard } from './organization.guards';

const access = (overrides: Partial<MyAccessResponse> = {}): MyAccessResponse => ({
  activeOrganizationId: 'org-a',
  organizations: [
    { id: 'org-a', name: 'Souza Advogados' },
    { id: 'org-b', name: 'Lima Advogados' },
  ],
  permissions: ['cases.read', 'settings.users.manage'],
  mfaRequired: false,
  ...overrides,
});

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string): Promise<ApiResult<never>> => Promise.resolve({ ok: false, code });

function setup(api: Partial<OrganizationApi>) {
  sessionStorage.clear();
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: OrganizationApi, useValue: { access: () => ok(access()), ...api } },
    ],
  });
  return { store: TestBed.inject(AccessStore), active: TestBed.inject(ActiveOrganization) };
}

describe('AccessStore', () => {
  it('loads once, pins the organization chosen by the server and answers permission checks', async () => {
    const calls: string[] = [];
    const { store, active } = setup({ access: () => (calls.push('access'), ok(access())) });

    await Promise.all([store.ensureLoaded(), store.ensureLoaded()]);
    await store.ensureLoaded();

    expect(calls).toEqual(['access']);
    expect(active.id()).toBe('org-a');
    expect(store.activeOrganization()?.name).toBe('Souza Advogados');
    expect(store.can('settings.users.manage')).toBe(true);
    expect(store.can('finance.invoices.approve')).toBe(false);
  });

  it('forgets an organization the user no longer belongs to and asks the server again', async () => {
    let attempt = 0;
    const { store, active } = setup({
      access: () =>
        ++attempt === 1
          ? fail('organization.not_a_member')
          : ok(access({ activeOrganizationId: 'org-b' })),
    });
    active.set('org-removed');

    await store.ensureLoaded();

    expect(attempt).toBe(2);
    expect(active.id()).toBe('org-b');
    expect(store.errorCode()).toBeNull();
  });

  it('switches organization on the server and reloads the permissions', async () => {
    const selected: string[] = [];
    const { store, active } = setup({
      select: (id: string) => (selected.push(id), ok(undefined)),
      access: () => ok(access({ activeOrganizationId: active.id() ?? 'org-a', permissions: [] })),
    });
    await store.ensureLoaded();

    expect(await store.switchTo('org-b')).toBe(true);

    expect(selected).toEqual(['org-b']);
    expect(active.id()).toBe('org-b');
    expect(store.activeOrganization()?.name).toBe('Lima Advogados');
    expect(store.can('cases.read')).toBe(false);
  });

  it('creates the first office and enters it, or keeps the error code', async () => {
    const { store, active } = setup({
      create: (name: string) =>
        name ? ok({ id: 'org-new', name }) : fail('organization.name_required'),
      access: () =>
        ok(
          access({
            activeOrganizationId: 'org-new',
            organizations: [{ id: 'org-new', name: 'Novo' }],
          }),
        ),
    });

    expect(await store.createOrganization('')).toBe(false);
    expect(store.errorCode()).toBe('organization.name_required');
    expect(await store.createOrganization('Novo')).toBe(true);
    expect(active.id()).toBe('org-new');

    store.reset();
    expect(active.id()).toBeNull();
    expect(store.loaded()).toBe(false);
  });
});

describe('organizationInterceptor', () => {
  it('sends X-Organization-Id only to the api', () => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: APP_CONFIG,
          useValue: { apiUrl: 'http://api.test', authority: 'http://id.test' },
        },
        provideHttpClient(withInterceptors([organizationInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    TestBed.inject(ActiveOrganization).set('org-a');
    const http = TestBed.inject(HttpClient);
    const backend = TestBed.inject(HttpTestingController);

    http.get('http://api.test/api/v1/roles').subscribe();
    http.get('http://elsewhere.test/x').subscribe();

    expect(
      backend.expectOne('http://api.test/api/v1/roles').request.headers.get(ORGANIZATION_HEADER),
    ).toBe('org-a');
    expect(
      backend.expectOne('http://elsewhere.test/x').request.headers.has(ORGANIZATION_HEADER),
    ).toBe(false);
    backend.verify();
  });
});

@Component({
  imports: [CanDirective],
  template: `<button *lfCan="permission()">Convidar</button>`,
})
class CanHost {
  readonly permission = signal('settings.users.manage');
}

describe('CanDirective', () => {
  it('shows the content only with the permission, and follows changes', async () => {
    const { store } = setup({});
    await store.ensureLoaded();
    const fixture = TestBed.createComponent(CanHost);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('button')?.textContent).toBe('Convidar');

    fixture.componentInstance.permission.set('finance.invoices.approve');
    await fixture.whenStable();
    expect(element.querySelector('button')).toBeNull();
  });
});

describe('organization guards', () => {
  const run = (guard: typeof organizationGuard) =>
    TestBed.runInInjectionContext(() =>
      guard({ path: '' }, [], {} as Parameters<typeof organizationGuard>[2]),
    ) as Promise<boolean | UrlTree>;

  it('sends users without an office to the onboarding', async () => {
    setup({
      access: () => ok(access({ activeOrganizationId: null, organizations: [], permissions: [] })),
    });
    const router = TestBed.inject(Router);

    const result = await run(organizationGuard);

    expect(router.serializeUrl(result as UrlTree)).toBe('/escritorio/novo');
  });

  it('opens permission routes only for those who have the permission', async () => {
    setup({});

    expect(await run(permissionGuard('settings.users.manage'))).toBe(true);
    expect(await run(organizationGuard)).toBe(true);
    const denied = await run(permissionGuard('finance.invoices.approve'));
    expect(TestBed.inject(Router).serializeUrl(denied as UrlTree)).toBe('/');
  });
});
