import { TestBed } from '@angular/core/testing';
import type {
  PublicationResponse,
  PublicationSummary,
  DailyCloseStatusResponse,
  NotificationsPageResponse,
} from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import { PublicationsApi, PENDING_FILTERS } from './publications-api';
import { PublicationsStore } from './publications-store';
import { ClosingStore } from './closing-store';
import { SourcesStore } from './sources-store';
import { MovementsStore } from './movements-store';
import { NotificationsApi } from './notifications-api';
import { NotificationStream } from './notification-stream';
import { PublicationsLiveStore } from './publications-live-store';
import { CaptureAlertsStore } from './capture-alerts-store';

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string): Promise<ApiResult<never>> => Promise.resolve({ ok: false, code });
function deferred<T>() {
  let resolve!: (value: ApiResult<T>) => void;
  const promise = new Promise<ApiResult<T>>((r) => {
    resolve = r;
  });
  return { promise, resolve };
}
function setup<T>(store: new () => T, api: Partial<PublicationsApi>): T {
  TestBed.configureTestingModule({
    providers: [store, { provide: PublicationsApi, useValue: api }],
  });
  return TestBed.inject(store);
}
const summary = (id: string): PublicationSummary => ({
  id,
  journal: id,
  publishedOn: '2026-10-19',
  extractedCnj: null,
  legalCaseId: null,
  status: 'pending',
  receivedAt: '2026-10-19T15:00:00Z',
});
const publication = (id: string): PublicationResponse => ({
  ...summary(id),
  sourceId: 's',
  text: '<script>unsafe</script>',
  availableOn: '2026-10-19',
});
const closing = (day = '2026-10-19'): DailyCloseStatusResponse => ({
  day,
  pendingCount: 0,
  missingCaptureCount: 0,
  publicationCount: 2,
  isClosed: false,
  latest: null,
  history: [],
  nextRevision: null,
});

describe('PublicationsStore', () => {
  it('ignores stale search results and sends the current filters', async () => {
    const slow = deferred<{ items: PublicationSummary[]; nextCursor: null }>();
    const list = vi
      .fn()
      .mockReturnValueOnce(slow.promise)
      .mockReturnValueOnce(ok({ items: [summary('new')], nextCursor: null }));
    const store = setup(PublicationsStore, { list });
    const first = store.search(PENDING_FILTERS);
    await store.search({ ...PENDING_FILTERS, linked: false });
    slow.resolve({ ok: true, value: { items: [summary('old')], nextCursor: null } });
    await first;
    expect(store.items().map((p) => p.id)).toEqual(['new']);
    expect(list).toHaveBeenLastCalledWith({ ...PENDING_FILTERS, linked: false }, null);
  });
  it('ignores detail responses after another selection or search', async () => {
    const slow = deferred<PublicationResponse>();
    const store = setup(PublicationsStore, {
      detail: vi
        .fn()
        .mockReturnValueOnce(slow.promise)
        .mockReturnValueOnce(ok(publication('new'))),
      list: () => ok({ items: [], nextCursor: null }),
    });
    const first = store.select('old');
    await store.select('new');
    slow.resolve({ ok: true, value: publication('old') });
    await first;
    expect(store.selected()?.id).toBe('new');
    await store.search(PENDING_FILTERS);
    expect(store.selected()).toBeNull();
  });
  it('does not append a stale page after filters change', async () => {
    const slow = deferred<{ items: PublicationSummary[]; nextCursor: null }>();
    const list = vi
      .fn()
      .mockReturnValueOnce(ok({ items: [summary('a')], nextCursor: 'next' }))
      .mockReturnValueOnce(slow.promise)
      .mockReturnValueOnce(ok({ items: [], nextCursor: null }));
    const store = setup(PublicationsStore, { list });
    await store.search(PENDING_FILTERS);
    const more = store.more();
    await store.search({ ...PENDING_FILTERS, status: 'triaged' });
    slow.resolve({ ok: true, value: { items: [summary('stale')], nextCursor: null } });
    await more;
    expect(store.items()).toEqual([]);
    expect(store.loading()).toBe(false);
  });
  it('keeps the selected publication and error when triage fails', async () => {
    const triage = vi.fn().mockReturnValue(fail('publications.case_required'));
    const store = setup(PublicationsStore, { detail: () => ok(publication('p')), triage });
    await store.select('p');
    expect(
      await store.triage('p', { classification: 'routine', action: 'noAction', confirmed: true }),
    ).toBe(false);
    expect(store.selected()?.id).toBe('p');
    expect(store.errorCode()).toBe('publications.case_required');
    expect(store.busy()).toBe(false);
  });
  it('refreshes after successful triage and guards duplicate submissions', async () => {
    const pending = deferred<never>();
    const triage = vi.fn().mockReturnValue(pending.promise);
    const store = setup(PublicationsStore, {
      triage,
      list: () => ok({ items: [], nextCursor: null }),
    });
    const input = {
      classification: 'other',
      action: 'discard',
      confirmed: true,
      reason: 'Mistaken identity',
    } as const;
    const first = store.triage('p', input);
    expect(await store.triage('p', input)).toBe(false);
    pending.resolve({ ok: false, code: 'publications.already_triaged' });
    await first;
    expect(triage).toHaveBeenCalledTimes(1);
  });
  it('assigns and links through the API and refreshes the queue', async () => {
    const assign = vi.fn().mockReturnValue(ok({}));
    const link = vi.fn().mockReturnValue(ok({}));
    const list = vi.fn().mockReturnValue(ok({ items: [], nextCursor: null }));
    const store = setup(PublicationsStore, { assign, link, list });
    expect(await store.assign('p', null)).toBe(true);
    expect(await store.link('p', 'c')).toBe(true);
    expect(assign).toHaveBeenCalledWith('p', null);
    expect(link).toHaveBeenCalledWith('p', 'c');
    expect(list).toHaveBeenCalledTimes(2);
  });
  it('keeps the selected form while refreshing a successful assignment', async () => {
    const pending = deferred<{ items: PublicationSummary[]; nextCursor: null }>();
    const detail = vi
      .fn()
      .mockReturnValueOnce(ok(publication('p')))
      .mockReturnValueOnce(ok({ ...publication('p'), assigneeUserId: 'u' }));
    const store = setup(PublicationsStore, {
      detail,
      assign: () => ok({} as never),
      list: () => pending.promise,
    });
    await store.select('p');
    const assigned = store.assign('p', 'u');
    await Promise.resolve();
    expect(store.selected()?.id).toBe('p');
    expect(store.busy()).toBe(true);
    pending.resolve({ ok: true, value: { items: [summary('p')], nextCursor: null } });
    await assigned;
    expect(store.selected()?.assigneeUserId).toBe('u');
    expect(store.busy()).toBe(false);
  });
  it('news preserves selected text and does not reload an edited publication', async () => {
    const store = setup(PublicationsStore, { detail: () => ok(publication('p')) });
    await store.select('p');
    store.news();
    expect(store.notice()).toBe(true);
    expect(store.selected()?.text).toContain('<script>');
  });
  it('uses only the newest case search options', async () => {
    const slow = deferred<{ members: []; cases: [] }>();
    const store = setup(PublicationsStore, {
      options: vi
        .fn()
        .mockReturnValueOnce(slow.promise)
        .mockReturnValueOnce(ok({ members: [], cases: [{ id: 'c', folder: '1', cnj: null }] })),
    });
    const first = store.loadOptions('old');
    await store.loadOptions('new');
    slow.resolve({ ok: true, value: { members: [], cases: [] } });
    await first;
    expect(store.options()?.cases[0].id).toBe('c');
  });
});

describe('ClosingStore', () => {
  it('discards stale dates and clears inaccessible state', async () => {
    const slow = deferred<DailyCloseStatusResponse>();
    const store = setup(ClosingStore, {
      closing: vi
        .fn()
        .mockReturnValueOnce(slow.promise)
        .mockReturnValueOnce(fail('publications.close_access_denied')),
    });
    const first = store.load('2026-10-18');
    await store.load('2026-10-19');
    slow.resolve({ ok: true, value: closing('2026-10-18') });
    await first;
    expect(store.state()).toBeNull();
    expect(store.errorCode()).toBe('publications.close_access_denied');
  });
  it('keeps history and state on rejected close', async () => {
    const store = setup(ClosingStore, {
      closing: () => ok(closing()),
      close: () => fail('publications.close_incomplete_capture'),
    });
    await store.load('2026-10-19');
    expect(await store.close()).toBe(false);
    expect(store.state()?.day).toBe('2026-10-19');
    expect(store.errorCode()).toBe('publications.close_incomplete_capture');
  });
  it('reloads the same day after signing and preserves signatures when loading more', async () => {
    const first = {
      id: 'one',
      day: '2026-10-19',
      revision: 2,
      closedBy: 'u',
      closedAt: '',
      publicationCount: 2,
    };
    const second = { ...first, id: 'two', revision: 1 };
    const api = {
      close: vi.fn().mockReturnValue(ok(first)),
      closing: vi
        .fn()
        .mockReturnValueOnce(ok({ ...closing(), history: [first], nextRevision: 2 }))
        .mockReturnValueOnce(ok({ ...closing(), history: [second] }))
        .mockReturnValueOnce(ok({ ...closing(), isClosed: true, history: [first, second] })),
    };
    const store = setup(ClosingStore, api);
    await store.load('2026-10-19');
    await store.more();
    expect(store.state()?.history.map((h) => h.id)).toEqual(['one', 'two']);
    expect(await store.close()).toBe(true);
    expect(store.state()?.isClosed).toBe(true);
  });
});

describe('SourcesStore', () => {
  it('loads sources and integration independently and reports failure', async () => {
    const store = setup(SourcesStore, {
      sources: () => ok([]),
      integration: () => fail('access.denied'),
    });
    await store.load();
    expect(store.items()).toEqual([]);
    expect(store.errorCode()).toBe('access.denied');
    expect(store.loading()).toBe(false);
  });
  it('preserves an integration when a rejected change occurs and never stores the key', async () => {
    const configure = vi
      .fn()
      .mockReturnValueOnce(ok({ provider: 'dataJud', hasApiKey: true }))
      .mockReturnValueOnce(fail('publications.credentials_invalid'));
    const store = setup(SourcesStore, { configure });
    expect(await store.configure({ provider: 'dataJud', apiKey: 'private-key' })).toBe(true);
    expect(JSON.stringify(store.integration())).not.toContain('private-key');
    expect(await store.configure({ provider: 'dataJud', clearApiKey: true })).toBe(false);
    expect(store.integration()?.hasApiKey).toBe(true);
  });
  it('reloads after source save and reports a rejected source', async () => {
    const saveSource = vi
      .fn()
      .mockReturnValueOnce(ok({}))
      .mockReturnValueOnce(fail('publications.source_invalid'));
    const sources = vi.fn().mockReturnValue(ok([]));
    const store = setup(SourcesStore, {
      saveSource,
      sources,
      integration: () => ok({ provider: 'disabled', hasApiKey: false }),
    });
    const input = { kind: 'name', label: 'Office', value: 'Office', isActive: true } as const;
    expect(await store.save(null, input)).toBe(true);
    expect(sources).toHaveBeenCalledTimes(1);
    expect(await store.save('s', input)).toBe(false);
    expect(store.errorCode()).toBe('publications.source_invalid');
  });
  it('ignores an obsolete capture response', async () => {
    const slow = deferred<never>();
    const store = setup(SourcesStore, {
      capture: vi
        .fn()
        .mockReturnValueOnce(slow.promise)
        .mockReturnValueOnce(fail('publications.source_not_found')),
    });
    const first = store.inspect('old');
    await store.inspect('new');
    slow.resolve({ ok: false, code: 'old' });
    await first;
    expect(store.errorCode()).toBe('publications.source_not_found');
  });
});

describe('MovementsStore', () => {
  it('loads and saves movements without removing draft state on error', async () => {
    const createMovement = vi
      .fn()
      .mockReturnValueOnce(fail('publications.movement_invalid'))
      .mockReturnValueOnce(ok({}));
    const store = setup(MovementsStore, {
      movements: () => ok({ items: [], nextCursor: null }),
      sync: () =>
        ok({
          caseId: 'c',
          tribunal: null,
          lastAttemptAt: null,
          lastSuccessAt: null,
          hasPendingPage: false,
          errorCode: null,
        }),
      createMovement,
    });
    await store.load('c');
    const input = { occurredAt: '2026-10-19T15:00:00Z', description: 'Court decision' };
    expect(await store.save(input)).toBe(false);
    expect(store.errorCode()).toBe('publications.movement_invalid');
    expect(await store.save(input)).toBe(true);
    expect(createMovement).toHaveBeenLastCalledWith('c', input);
  });
  it('does not expose the previous case after access is denied', async () => {
    const store = setup(MovementsStore, {
      movements: () => fail('cases.not_found'),
      sync: () => fail('cases.not_found'),
    });
    await store.load('hidden');
    expect(store.items()).toEqual([]);
    expect(store.sync()).toBeNull();
    expect(store.errorCode()).toBe('cases.not_found');
  });
});

describe('PublicationsLiveStore', () => {
  function liveSetup() {
    const page: NotificationsPageResponse = {
      items: [
        { id: 'n', kind: 'publications.received', resourceId: 'p', createdAt: '', readAt: null },
      ],
      nextCursor: null,
    };
    let snapshot!: (page: NotificationsPageResponse) => void;
    let online!: (value: boolean) => void;
    const dispose = vi.fn();
    const list = vi.fn().mockReturnValue(ok(page));
    const read = vi.fn().mockReturnValue(ok(undefined));
    const pendingCount = vi.fn().mockReturnValue(ok({ count: 300 }));
    const watch = vi.fn((next, state) => {
      snapshot = next;
      online = state;
      return dispose;
    });
    TestBed.configureTestingModule({
      providers: [
        { provide: NotificationsApi, useValue: { list, read } },
        { provide: PublicationsApi, useValue: { pendingCount } },
        { provide: NotificationStream, useValue: { watch } },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    return {
      store: TestBed.inject(PublicationsLiveStore),
      list,
      read,
      pendingCount,
      watch,
      dispose,
      page,
      snapshot: (value: NotificationsPageResponse) => snapshot(value),
      online: (value: boolean) => online(value),
    };
  }
  it('loads the exact pending count independently of the 50-notification snapshot', async () => {
    const t = liveSetup();
    t.store.start();
    await t.store.refresh();
    expect(t.store.pendingCount()).toBe(300);
    expect(t.store.items()).toHaveLength(1);
    t.store.start();
    expect(t.watch).toHaveBeenCalledTimes(1);
    t.store.stop();
  });
  it('invalidates the queue on new snapshots and clears all data on stop', async () => {
    const t = liveSetup();
    t.store.start();
    await t.store.refresh();
    t.snapshot(t.page);
    t.online(true);
    expect(t.store.revision()).toBe(1);
    expect(t.store.online()).toBe(true);
    t.store.stop();
    expect(t.dispose).toHaveBeenCalledTimes(1);
    expect(t.store.items()).toEqual([]);
    expect(t.store.pendingCount()).toBeNull();
    t.snapshot(t.page);
    expect(t.store.items()).toEqual([]);
  });
  it('marks a notification read and refreshes persisted state', async () => {
    const t = liveSetup();
    t.store.start();
    expect(await t.store.read('n')).toBe(true);
    expect(t.read).toHaveBeenCalledWith('n');
    t.store.stop();
  });
  it('does not let older REST data replace a newer stream snapshot', async () => {
    const t = liveSetup();
    const pending = deferred<NotificationsPageResponse>();
    t.list.mockReturnValueOnce(pending.promise);
    t.store.start();
    const fresh = { ...t.page, items: [{ ...t.page.items[0], id: 'new' }] };
    t.snapshot(fresh);
    pending.resolve({ ok: true, value: t.page });
    await pending.promise;
    await Promise.resolve();
    expect(t.store.items()[0].id).toBe('new');
    t.store.stop();
  });
  it('keeps the newest count when a slower request finishes later', async () => {
    const t = liveSetup();
    const pending = deferred<{ count: number }>();
    t.pendingCount.mockReturnValueOnce(pending.promise).mockReturnValueOnce(ok({ count: 0 }));
    t.store.start();
    await Promise.resolve();
    await t.store.refreshCount();
    pending.resolve({ ok: true, value: { count: 300 } });
    await pending.promise;
    await Promise.resolve();
    expect(t.store.pendingCount()).toBe(0);
    t.store.stop();
  });
  it('still counts publications without permission to read notifications', async () => {
    const t = liveSetup();
    vi.spyOn(TestBed.inject(AccessStore), 'can').mockImplementation(
      (permission) => permission === 'publications.read',
    );
    t.store.start();
    await t.store.refreshCount();
    expect(t.store.pendingCount()).toBe(300);
    expect(t.list).not.toHaveBeenCalled();
    expect(t.watch).not.toHaveBeenCalled();
    t.store.stop();
  });
  it('discards REST data that returns after logout', async () => {
    const t = liveSetup();
    const pending = deferred<NotificationsPageResponse>();
    t.list.mockReturnValue(pending.promise);
    t.store.start();
    t.store.stop();
    pending.resolve({ ok: true, value: t.page });
    await pending.promise;
    await Promise.resolve();
    expect(t.store.items()).toEqual([]);
    expect(t.store.pendingCount()).toBeNull();
  });
});

describe('CaptureAlertsStore', () => {
  it('does not retain alerts outside the current authorized day', async () => {
    const slow = deferred<never>();
    const store = setup(CaptureAlertsStore, {
      alerts: vi.fn().mockReturnValueOnce(slow.promise).mockReturnValueOnce(fail('access.denied')),
    });
    const first = store.load('2026-10-18');
    await store.load('2026-10-19');
    slow.resolve({ ok: false, code: 'old' });
    await first;
    expect(store.items()).toEqual([]);
    expect(store.errorCode()).toBe('access.denied');
    expect(store.loading()).toBe(false);
  });
  it('loads a successful capture alert query', async () => {
    const alerts = vi.fn().mockReturnValue(ok([]));
    const store = setup(CaptureAlertsStore, { alerts });
    await store.load('2026-10-19');
    expect(alerts).toHaveBeenCalledWith('2026-10-19');
    expect(store.errorCode()).toBeNull();
  });
});

describe('Manual capture requests', () => {
  it('records the accepted request and keeps the chosen day', async () => {
    const retryCapture = vi.fn().mockReturnValue(ok({ requestId: 'r', day: '2026-10-18' }));
    const store = setup(SourcesStore, { retryCapture });
    expect(await store.retry('s', '2026-10-18')).toBe(true);
    expect(store.requested()?.day).toBe('2026-10-18');
    expect(retryCapture).toHaveBeenCalledWith('s', '2026-10-18');
  });
  it('keeps a rejected retry distinct from a completed capture', async () => {
    const store = setup(SourcesStore, { retryCapture: () => fail('publications.source_inactive') });
    expect(await store.retry('s', '2026-10-19')).toBe(false);
    expect(store.requested()).toBeNull();
    expect(store.capture()).toBeNull();
    expect(store.errorCode()).toBe('publications.source_inactive');
  });
});
