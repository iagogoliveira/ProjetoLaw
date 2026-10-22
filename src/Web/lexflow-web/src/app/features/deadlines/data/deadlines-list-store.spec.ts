import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { DeadlineListItem, DeadlineListPage } from '../../../api/types.gen';
import { DeadlinesApi, type DeadlineFilters } from './deadlines-api';
import { DeadlinesListStore, readDeadlineListPreference } from './deadlines-list-store';

const filters: DeadlineFilters = {
  from: '2026-10-01',
  until: '2026-11-01',
  assigneeUserId: null,
  teamId: null,
  includeClosed: false,
};
const item: DeadlineListItem = {
  id: 'deadline-1',
  caseId: 'case-1',
  type: 'court',
  title: 'Prazo',
  startOn: '2026-10-01',
  internalDueOn: '2026-10-20',
  fatalDueOn: '2026-10-22',
  assigneeUserId: 'user-1',
  status: 'pending',
  requireReview: false,
  reviewedAt: null,
};
const page = (items: DeadlineListItem[], nextCursor: string | null = null): DeadlineListPage => ({
  today: '2026-10-21',
  items,
  nextCursor,
});

describe('DeadlinesListStore', () => {
  function setup(api: Record<string, ReturnType<typeof vi.fn>>) {
    TestBed.configureTestingModule({
      providers: [DeadlinesListStore, { provide: DeadlinesApi, useValue: api }],
    });
    return TestBed.inject(DeadlinesListStore);
  }

  it('ignores stale searches and keeps only the newest response', async () => {
    const resolvers: ((response: { ok: true; value: DeadlineListPage }) => void)[] = [];
    const list = vi.fn(
      () =>
        new Promise<{ ok: true; value: DeadlineListPage }>((resolve) => resolvers.push(resolve)),
    );
    const store = setup({ list });
    const oldFilters = { ...filters, caseId: 'old-case' };
    const currentFilters = { ...filters, caseId: 'current-case' };
    const older = store.search(oldFilters);
    const newer = store.search(currentFilters);
    resolvers[1]({ ok: true, value: page([{ ...item, id: 'newest' }]) });
    await newer;
    resolvers[0]({ ok: true, value: page([{ ...item, id: 'stale' }]) });
    await older;
    expect(store.items().map((row) => row.id)).toEqual(['newest']);
    expect(store.filters()?.caseId).toBe('current-case');
  });

  it('clears the pagination cursor after a failed search', async () => {
    const list = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, value: page([item], 'cursor-1') })
      .mockResolvedValueOnce({ ok: false, code: 'scheduling.deadline_list_invalid' });
    const store = setup({ list });
    await store.search(filters);
    expect(store.nextCursor()).toBe('cursor-1');
    await store.search({ ...filters, from: 'invalid' });
    expect(store.items()).toEqual([]);
    expect(store.nextCursor()).toBeNull();
    await store.loadMore();
    expect(list).toHaveBeenCalledTimes(2);
  });

  it('validates saved preference JSON, deduplicates names, and limits the list', () => {
    const valid = Array.from({ length: 12 }, (_, index) => ({ name: `Filter ${index}`, filters }));
    const malformed = [{ name: 'bad date', filters: { ...filters, from: 'yesterday' } }, ...valid];
    const result = readDeadlineListPreference({ saved: malformed });
    expect(result).toHaveLength(10);
    expect(result[0]?.name).toBe('Filter 0');
    expect(result.some((entry) => entry.name === 'bad date')).toBe(false);
    expect(
      readDeadlineListPreference({
        saved: [
          { name: 'Same', filters },
          { name: 'Same', filters },
        ],
      }),
    ).toHaveLength(1);
    expect(readDeadlineListPreference('invalid')).toEqual([]);
  });

  it('persists a saved filter only when saveFilter is explicitly called', async () => {
    const savePreference = vi.fn().mockResolvedValue({ ok: true, value: {} });
    const store = setup({
      list: vi.fn().mockResolvedValue({ ok: true, value: page([]) }),
      savePreference,
      preference: vi.fn().mockResolvedValue({ ok: true, value: { value: { saved: [] } } }),
    });
    await store.search(filters);
    expect(savePreference).not.toHaveBeenCalled();
    await store.loadSaved();
    await store.saveFilter('My deadlines');
    expect(savePreference).toHaveBeenCalledWith({ saved: [{ name: 'My deadlines', filters }] });
    expect(store.saved()).toEqual([{ name: 'My deadlines', filters }]);
  });

  it('allows the first filter after the server confirms no preference exists', async () => {
    const savePreference = vi.fn().mockResolvedValue({ ok: true, value: {} });
    const store = setup({
      preference: vi.fn().mockResolvedValue({ ok: false, code: 'platform.preference_not_found' }),
      savePreference,
      list: vi.fn().mockResolvedValue({ ok: true, value: page([]) }),
    });
    await store.search(filters);
    await store.loadSaved();
    expect(store.savedReady()).toBe(true);
    expect(store.preferenceError()).toBeNull();
    expect(await store.saveFilter('First')).toBe(true);
    expect(savePreference).toHaveBeenCalledWith({ saved: [{ name: 'First', filters }] });
  });

  it('blocks writes while preferences are pending or failed and preserves existing filters after retry', async () => {
    let finish!: (value: { ok: false; code: string }) => void;
    const preference = vi
      .fn()
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            finish = resolve;
          }),
      )
      .mockResolvedValueOnce({
        ok: true,
        value: { value: { saved: [{ name: 'Existing', filters }] } },
      });
    const savePreference = vi.fn().mockResolvedValue({ ok: true, value: {} });
    const store = setup({
      preference,
      savePreference,
      list: vi.fn().mockResolvedValue({ ok: true, value: page([]) }),
    });
    await store.search(filters);
    const reading = store.loadSaved();
    expect(await store.saveFilter('New')).toBe(false);
    expect(savePreference).not.toHaveBeenCalled();
    finish({ ok: false, code: 'http.error' });
    await reading;
    expect(await store.saveFilter('New')).toBe(false);
    expect(savePreference).not.toHaveBeenCalled();
    await store.loadSaved();
    expect(await store.saveFilter('New')).toBe(true);
    expect(savePreference).toHaveBeenCalledWith({
      saved: [
        { name: 'Existing', filters: { ...filters, caseId: null } },
        { name: 'New', filters },
      ],
    });
  });

  it('ignores a preference response older than the successfully saved filter set', async () => {
    let older!: (value: {
      ok: true;
      value: { value: { saved: { name: string; filters: DeadlineFilters }[] } };
    }) => void;
    const preference = vi
      .fn()
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            older = resolve;
          }),
      )
      .mockResolvedValueOnce({
        ok: true,
        value: { value: { saved: [{ name: 'Current', filters }] } },
      });
    const store = setup({
      preference,
      savePreference: vi.fn().mockResolvedValue({ ok: true, value: {} }),
      list: vi.fn().mockResolvedValue({ ok: true, value: page([]) }),
    });
    await store.search(filters);
    const stale = store.loadSaved();
    await store.loadSaved();
    await store.saveFilter('New');
    older({ ok: true, value: { value: { saved: [{ name: 'Stale', filters }] } } });
    await stale;
    expect(store.saved().map((saved) => saved.name)).toEqual(['Current', 'New']);
    expect(store.savedReady()).toBe(true);
  });
});
