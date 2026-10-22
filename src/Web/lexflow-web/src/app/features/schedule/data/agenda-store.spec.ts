import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { AgendaItem, AgendaPage } from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { AgendaStore } from './agenda-store';
import { ScheduleApi } from './schedule-api';
import { AgendaPreferencesStore } from './agenda-preferences-store';

const filters = {
  from: '2026-10-22',
  until: '2026-10-23',
  assigneeUserId: null,
  teamId: null,
  includeClosed: false,
};
const item: AgendaItem = {
  kind: 'event',
  id: 'one',
  title: 'Meeting',
  caseId: null,
  startsAt: '2026-10-22T12:00:00Z',
  endsAt: '2026-10-22T13:00:00Z',
  assigneeUserId: null,
  status: 'scheduled',
  allDay: false,
  internalDueOn: null,
};
describe('Agenda queries and preferences', () => {
  it('rejects old responses and resets pagination after failure instead of skipping earlier rows', async () => {
    let resolve!: (value: ApiResult<AgendaPage>) => void;
    const first = new Promise<ApiResult<AgendaPage>>((done) => (resolve = done));
    const agenda = vi
      .fn()
      .mockReturnValueOnce(first)
      .mockResolvedValueOnce({ ok: true, value: { items: [item], nextCursor: 'next' } })
      .mockResolvedValueOnce({ ok: false, code: 'auth.forbidden' });
    TestBed.configureTestingModule({
      providers: [AgendaStore, { provide: ScheduleApi, useValue: { agenda } }],
    });
    const store = TestBed.inject(AgendaStore);
    const old = store.search(filters);
    await store.search({ ...filters, includeClosed: true });
    resolve({ ok: true, value: { items: [{ ...item, id: 'old' }], nextCursor: null } });
    await old;
    expect(store.items()).toEqual([item]);
    await store.search(filters, true);
    expect(store.items()).toEqual([]);
    expect(store.cursor()).toBeNull();
    expect(store.loading()).toBe(false);
  });
  it('never overwrites saved filters after a failed read and permits the first known-empty preference', async () => {
    const preference = vi
      .fn()
      .mockResolvedValueOnce({ ok: false, code: 'http.error' })
      .mockResolvedValueOnce({ ok: false, code: 'platform.preference_not_found' });
    const savePreference = vi.fn().mockResolvedValue({ ok: true, value: {} });
    TestBed.configureTestingModule({
      providers: [
        AgendaPreferencesStore,
        { provide: ScheduleApi, useValue: { preference, savePreference } },
      ],
    });
    const store = TestBed.inject(AgendaPreferencesStore);
    await store.load();
    await store.save('Work', filters);
    expect(savePreference).not.toHaveBeenCalled();
    await store.load();
    await store.save('Work', filters);
    expect(savePreference).toHaveBeenCalledTimes(1);
    expect(store.saved()[0].name).toBe('Work');
  });
});
