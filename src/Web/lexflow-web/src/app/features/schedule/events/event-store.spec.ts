import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { CalendarEventResponse } from '../../../api/types.gen';
import { ScheduleApi } from '../data/schedule-api';
import { EventStore } from './event-store';

const event = (id: string): CalendarEventResponse => ({
  id,
  caseId: null,
  title: `Event ${id}`,
  description: null,
  startsAt: '2026-10-22T13:00:00Z',
  endsAt: '2026-10-22T14:00:00Z',
  assigneeUserId: 'member-1',
  location: null,
  status: 'scheduled',
});

describe('EventStore', () => {
  function setup(api: Record<string, ReturnType<typeof vi.fn>>) {
    TestBed.configureTestingModule({
      providers: [EventStore, { provide: ScheduleApi, useValue: api }],
    });
    return TestBed.inject(EventStore);
  }

  it('ignores a late response for a previous route event', async () => {
    const pending: ((result: { ok: true; value: CalendarEventResponse }) => void)[] = [];
    const store = setup({ event: vi.fn(() => new Promise((resolve) => pending.push(resolve))) });
    const first = store.load('first', false);
    const second = store.load('second', false);
    pending[1]!({ ok: true, value: event('second') });
    await second;
    pending[0]!({ ok: true, value: event('first') });
    await first;
    expect(store.item()?.id).toBe('second');
  });

  it('clears event data when the API reports that access is denied', async () => {
    const store = setup({
      event: vi.fn().mockResolvedValue({ ok: false, code: 'authorization.forbidden' }),
    });
    await store.load('private-event', false);
    expect(store.item()).toBeNull();
    expect(store.error()).toBe('authorization.forbidden');
  });
});
