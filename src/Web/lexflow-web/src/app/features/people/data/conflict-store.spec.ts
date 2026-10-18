import { TestBed } from '@angular/core/testing';
import type { ConflictCheckResponse } from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { ConflictStore } from './conflict-store';
import { CrmApi } from './crm-api';

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string): Promise<ApiResult<never>> => Promise.resolve({ ok: false, code });

const check = (
  id: string,
  overrides: Partial<ConflictCheckResponse> = {},
): ConflictCheckResponse => ({
  id,
  queryName: 'Empresa Adversária',
  queryDocument: null,
  side: 'client',
  risk: 'direct',
  proceeded: false,
  proceededAt: null,
  justification: null,
  checkedBy: 'u1',
  createdAt: '2026-10-18T12:00:00Z',
  hits: [
    {
      risk: 'direct',
      reason: 'sameName',
      personId: 'p1',
      matchedName: 'Empresa Adversária',
      caseNumber: null,
      score: null,
    },
  ],
  ...overrides,
});

function setup(api: Partial<CrmApi>) {
  TestBed.configureTestingModule({
    providers: [ConflictStore, { provide: CrmApi, useValue: api }],
  });
  return TestBed.inject(ConflictStore);
}

describe('ConflictStore', () => {
  it('checks and proceeds with the trimmed justification', async () => {
    const proceed = vi.fn(() => ok(check('c1', { proceeded: true, justification: 'Sem relação' })));
    const store = setup({ checkConflict: () => ok(check('c1')), proceed });

    await store.check({
      name: 'Empresa Adversária',
      document: null,
      side: 'client',
      contactEmails: [],
    });
    expect(store.result()?.risk).toBe('direct');

    expect(await store.proceed('  Sem relação  ')).toBe(true);
    expect(proceed).toHaveBeenCalledWith('c1', 'Sem relação');
    expect(store.result()?.proceeded).toBe(true);
  });

  it('keeps the error when proceeding is refused and can start over', async () => {
    const store = setup({
      checkConflict: () => ok(check('c1')),
      proceed: () => fail('crm.conflict_justification_required'),
    });
    await store.check({ name: 'X', document: null, side: 'opponent', contactEmails: [] });

    expect(await store.proceed('')).toBe(false);
    expect(store.errorCode()).toBe('crm.conflict_justification_required');
    store.reset();
    expect(store.result()).toBeNull();
    expect(store.errorCode()).toBeNull();
  });

  it('pages the report with the chosen risk', async () => {
    const conflictChecks = vi
      .fn()
      .mockReturnValueOnce(ok({ items: [check('c1')], nextCursor: 'k1' }))
      .mockReturnValueOnce(ok({ items: [check('c2')], nextCursor: null }));
    const store = setup({ conflictChecks });

    await store.loadReport('direct');
    await store.moreReport();

    expect(conflictChecks).toHaveBeenNthCalledWith(1, 'direct', null);
    expect(conflictChecks).toHaveBeenNthCalledWith(2, 'direct', 'k1');
    expect(store.report().map((c) => c.id)).toEqual(['c1', 'c2']);
    expect(store.nextCursor()).toBeNull();
  });
});
