import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type { ControlReportResponse } from '../../../api/types.gen';
import { ControlApi } from './control-api';
import { ControlStore, readControlPreference } from './control-store';

const filters = { from: '2026-10-01', until: '2026-11-01', idleDays: 30 };
const report: ControlReportResponse = {
  asOf: '2026-10-22T12:00:00Z',
  from: filters.from,
  until: filters.until,
  idleDays: 30,
  sections: [],
  metrics: [],
};
describe('ControlStore', () => {
  function setup(api: Record<string, ReturnType<typeof vi.fn>>) {
    TestBed.configureTestingModule({
      providers: [ControlStore, { provide: ControlApi, useValue: api }],
    });
    return TestBed.inject(ControlStore);
  }
  it('rejects invalid civil windows and keeps at most ten unique saved filters', () => {
    const saved = Array.from({ length: 11 }, (_, i) => ({ name: `Filter ${i}`, filters }));
    const parsed = readControlPreference({
      saved: [{ name: 'Bad', filters: { ...filters, until: '2026-02-30' } }, ...saved, saved[0]],
    });
    expect(parsed).toHaveLength(10);
    expect(parsed.some((item) => item.name === 'Bad')).toBe(false);
    expect(parsed.map((item) => item.name)).toEqual(
      Array.from({ length: 10 }, (_, i) => `Filter ${i}`),
    );
    expect(readControlPreference(null)).toEqual([]);
  });
  it('ignores an older report response after a newer filter query', async () => {
    const completions: ((value: { ok: true; value: ControlReportResponse }) => void)[] = [];
    const store = setup({
      report: vi.fn(() => new Promise((resolve) => completions.push(resolve))),
    });
    const old = store.search(filters);
    const newest = store.search({ ...filters, from: '2026-10-02' });
    completions[1]!({ ok: true, value: { ...report, from: '2026-10-02' } });
    await newest;
    completions[0]!({ ok: true, value: report });
    await old;
    expect(store.report()?.from).toBe('2026-10-02');
  });
  it('does not overwrite saved filters after a preference read error', async () => {
    const api = {
      preference: vi.fn().mockResolvedValue({ ok: false, code: 'http.error' }),
      savePreference: vi.fn(),
    };
    const store = setup(api);
    await store.loadSaved();
    expect(store.savedReady()).toBe(false);
    expect(store.preferenceError()).toBe('http.error');
    expect(await store.save('name')).toBe(false);
    expect(api.savePreference).not.toHaveBeenCalled();
  });
});
