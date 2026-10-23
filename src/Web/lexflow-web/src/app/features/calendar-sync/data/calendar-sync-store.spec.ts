import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { CalendarConnectionResponse, CalendarPendingResponse } from '../../../api/types.gen';
import { CalendarSyncApi } from './calendar-sync-api';
import { CalendarSyncStore } from './calendar-sync-store';

const connection = (id: string, isActive = true): CalendarConnectionResponse => ({
  id,
  provider: 'google',
  from: '2026-01-01',
  until: '2027-01-01',
  isActive,
  lastSyncedAt: null,
  lastErrorCode: null,
});
const pending = (id: string): CalendarPendingResponse => ({
  id,
  kind: 'event',
  localId: id,
  reason: 'conflict',
  localTitle: 'Local',
  localStartsAt: null,
  localEndsAt: null,
  externalTitle: 'Remote',
  externalStartsAt: '2026-01-01T12:00:00Z',
  externalEndsAt: '2026-01-01T13:00:00Z',
  externalCancelled: false,
  canAcceptExternal: true,
});
const ok = <T>(value: T) => Promise.resolve({ ok: true as const, value });
const failed = (code: string) => Promise.resolve({ ok: false as const, code });
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => (resolve = done));
  return { promise, resolve };
}

describe('CalendarSyncStore', () => {
  it('ignores an older pending response after selecting another connection', async () => {
    const oldPage = deferred<Awaited<ReturnType<CalendarSyncApi['pending']>>>();
    const api = {
      connections: vi.fn(() =>
        ok({
          isSimulation: true,
          isAvailable: true,
          items: [connection('one'), connection('two')],
        }),
      ),
      pending: vi.fn((id: string) =>
        id === 'one' ? oldPage.promise : ok({ items: [pending('two-item')], nextCursor: null }),
      ),
    };
    await TestBed.configureTestingModule({
      providers: [CalendarSyncStore, { provide: CalendarSyncApi, useValue: api }],
    }).compileComponents();
    const store = TestBed.inject(CalendarSyncStore);
    await store.loadConnections();
    const first = store.select('one');
    await store.select('two');
    oldPage.resolve({ ok: true, value: { items: [pending('one-item')], nextCursor: null } });
    await first;
    expect(store.selectedId()).toBe('two');
    expect(store.pending().map((item) => item.id)).toEqual(['two-item']);
  });

  it('keeps the cursor on page errors so the user can retry without losing loaded items', async () => {
    const api = {
      connections: vi.fn(() =>
        ok({ isSimulation: true, isAvailable: true, items: [connection('one')] }),
      ),
      pending: vi
        .fn()
        .mockResolvedValueOnce({
          ok: true,
          value: { items: [pending('first')], nextCursor: 'cursor-1' },
        })
        .mockImplementationOnce(() => failed('http.error'))
        .mockResolvedValueOnce({
          ok: true,
          value: { items: [pending('second')], nextCursor: null },
        }),
    };
    await TestBed.configureTestingModule({
      providers: [CalendarSyncStore, { provide: CalendarSyncApi, useValue: api }],
    }).compileComponents();
    const store = TestBed.inject(CalendarSyncStore);
    await store.loadConnections();
    await store.select('one');
    await store.loadMorePending();
    expect(store.pending().map((item) => item.id)).toEqual(['first']);
    expect(store.pendingCursor()).toBe('cursor-1');
    expect(store.pendingError()).toBe('http.error');
    await store.loadMorePending();
    expect(store.pending().map((item) => item.id)).toEqual(['first', 'second']);
  });

  it('treats a successful queue response as queued and prevents duplicate actions', async () => {
    const action = deferred<Awaited<ReturnType<CalendarSyncApi['queueSync']>>>();
    const api = {
      connections: vi.fn().mockResolvedValue({
        ok: true,
        value: { isSimulation: true, isAvailable: true, items: [connection('one')] },
      }),
      pending: vi.fn(() => ok({ items: [], nextCursor: null })),
      queueSync: vi.fn(() => action.promise),
    };
    await TestBed.configureTestingModule({
      providers: [CalendarSyncStore, { provide: CalendarSyncApi, useValue: api }],
    }).compileComponents();
    const store = TestBed.inject(CalendarSyncStore);
    await store.loadConnections();
    await store.select('one');
    const first = store.queueSync('one');
    expect(await store.queueSync('one')).toBe(false);
    action.resolve({ ok: true, value: connection('one') });
    expect(await first).toBe(true);
    expect(store.queued()).toBe(true);
  });

  it('clears the selected context and pending items on forbidden or missing resources', async () => {
    const api = {
      connections: vi.fn(() =>
        ok({ isSimulation: true, isAvailable: true, items: [connection('one')] }),
      ),
      pending: vi.fn(() => failed('scheduling.calendar_connection_not_found')),
    };
    await TestBed.configureTestingModule({
      providers: [CalendarSyncStore, { provide: CalendarSyncApi, useValue: api }],
    }).compileComponents();
    const store = TestBed.inject(CalendarSyncStore);
    await store.loadConnections();
    await store.select('one');
    expect(store.selectedId()).toBeNull();
    expect(store.pending()).toEqual([]);
    expect(store.pendingCursor()).toBeNull();
  });

  it('does not let an old connection action clear a newer action busy state', async () => {
    const oldAction = deferred<Awaited<ReturnType<CalendarSyncApi['disconnect']>>>();
    const newAction = deferred<Awaited<ReturnType<CalendarSyncApi['queueSync']>>>();
    const api = {
      connections: vi.fn(() =>
        ok({
          isSimulation: true,
          isAvailable: true,
          items: [connection('one'), connection('two')],
        }),
      ),
      pending: vi.fn(() => ok({ items: [], nextCursor: null })),
      disconnect: vi.fn(() => oldAction.promise),
      queueSync: vi.fn(() => newAction.promise),
    };
    await TestBed.configureTestingModule({
      providers: [CalendarSyncStore, { provide: CalendarSyncApi, useValue: api }],
    }).compileComponents();
    const store = TestBed.inject(CalendarSyncStore);
    await store.loadConnections();
    await store.select('one');
    const oldRun = store.disconnect('one');
    await store.select('two');
    const newRun = store.queueSync('two');
    oldAction.resolve({ ok: true, value: connection('one', false) });
    expect(await oldRun).toBe(false);
    expect(store.busy()).toBe(true);
    newAction.resolve({ ok: true, value: connection('two') });
    expect(await newRun).toBe(true);
    expect(store.busy()).toBe(false);
  });

  it('does not let an old resolution clear a newer action busy state', async () => {
    const oldResolution = deferred<Awaited<ReturnType<CalendarSyncApi['resolve']>>>();
    const newAction = deferred<Awaited<ReturnType<CalendarSyncApi['queueSync']>>>();
    const api = {
      connections: vi.fn(() =>
        ok({
          isSimulation: true,
          isAvailable: true,
          items: [connection('one'), connection('two')],
        }),
      ),
      pending: vi.fn(() => ok({ items: [], nextCursor: null })),
      resolve: vi.fn(() => oldResolution.promise),
      queueSync: vi.fn(() => newAction.promise),
    };
    await TestBed.configureTestingModule({
      providers: [CalendarSyncStore, { provide: CalendarSyncApi, useValue: api }],
    }).compileComponents();
    const store = TestBed.inject(CalendarSyncStore);
    await store.loadConnections();
    await store.select('one');
    const oldRun = store.resolve('one', 'pending-one', 'keepLocal');
    await store.select('two');
    const newRun = store.queueSync('two');
    oldResolution.resolve({ ok: true, value: { pendingId: 'pending-one', resolved: true } });
    expect(await oldRun).toBeNull();
    expect(store.busy()).toBe(true);
    newAction.resolve({ ok: true, value: connection('two') });
    expect(await newRun).toBe(true);
    expect(store.busy()).toBe(false);
  });
});
