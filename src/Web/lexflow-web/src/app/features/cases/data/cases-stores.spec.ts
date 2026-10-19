import { TestBed } from '@angular/core/testing';
import type {
  CaseOptionsResponse,
  CaseResponse,
  CasesPageResponse,
  CaseSummary,
  TimelineEntry,
} from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { CaseDetailStore, CaseEditorStore, NumberingStore } from './case-stores';
import { CasesApi, NO_FILTERS } from './cases-api';
import { CasesListStore, DEFAULT_COLUMNS, PREFERENCE_KEY } from './cases-list-store';
import { formatCnj } from './cases-format';

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string) => Promise.resolve<ApiResult<never>>({ ok: false, code });

const summary = (id: string): CaseSummary => ({
  id,
  kind: 'judicial',
  internalFolder: `CIV-2026-0000${id}`,
  cnjNumber: '22380589820258268434',
  status: 'active',
  practiceAreaId: null,
  phaseId: null,
  clients: ['Banco Alfa S.A.'],
  createdAt: '2026-10-18T12:00:00Z',
});

const page = (items: CaseSummary[], nextCursor: string | null = null): CasesPageResponse => ({
  items,
  nextCursor,
});

const options: CaseOptionsResponse = {
  practiceAreas: [{ id: 'a1', name: 'Cível' }],
  branches: [],
  teams: [],
  members: [{ userId: 'u1', name: 'Ana', email: 'ana@exemplo.com.br' }],
  phases: [{ id: 'f1', name: 'Inicial' }],
  actionTypes: [],
  closingReasons: [{ id: 'r1', name: 'Acordo' }],
  claimTypes: [],
};

const legalCase = (overrides: Partial<CaseResponse> = {}): CaseResponse => ({
  id: 'c1',
  kind: 'judicial',
  internalFolder: 'CIV-2026-00001',
  cnjNumber: '2238058-98.2025.8.26.8434',
  court: null,
  instance: 'first',
  district: null,
  courtUnit: null,
  rite: null,
  proceduralClass: null,
  subject: null,
  actionTypeId: null,
  practiceAreaId: 'a1',
  branchId: null,
  phaseId: null,
  status: 'active',
  claimValue: null,
  filedOn: null,
  servedOn: null,
  description: null,
  payerPersonId: null,
  parties: [],
  assignments: [],
  parent: null,
  children: [],
  closedOn: null,
  closingReasonId: null,
  outcome: null,
  finalAmount: null,
  closingNote: null,
  customFields: {},
  createdAt: '2026-10-18T12:00:00Z',
  isRestricted: false,
  ...overrides,
});

const entry = (id: string): TimelineEntry => ({
  id,
  at: '2026-10-18T12:00:00Z',
  kind: 'litigation.case_created',
  data: {},
  referenceId: null,
});

function setup<T>(store: new () => T, api: Partial<CasesApi>): T {
  TestBed.configureTestingModule({
    providers: [
      store,
      {
        provide: CasesApi,
        useValue: { options: () => ok(options), ...api },
      },
    ],
  });
  return TestBed.inject(store);
}

describe('cases format', () => {
  it('formats the CNJ digits and keeps other values', () => {
    expect(formatCnj('22380589820258268434')).toBe('2238058-98.2025.8.26.8434');
    expect(formatCnj('2238058-98.2025.8.26.8434')).toBe('2238058-98.2025.8.26.8434');
    expect(formatCnj(null)).toBe('');
  });
});

describe('CasesListStore', () => {
  it('starts with the saved columns and filters, or the defaults when there are none', async () => {
    const list = vi.fn(() => ok(page([summary('1')], 'next')));
    const withPreference = setup(CasesListStore, {
      list,
      preference: () =>
        ok({
          key: PREFERENCE_KEY,
          value: {
            columns: ['cnj', 'unknown'],
            saved: [{ name: 'Ativos', filters: { ...NO_FILTERS, status: 'active' } }],
          },
          updatedAt: '',
        }),
    });

    await withPreference.init();

    expect(withPreference.columns()).toEqual(['cnj']);
    expect(withPreference.saved()[0].name).toBe('Ativos');
    expect(withPreference.items().length).toBe(1);
    expect(withPreference.options()?.phases[0].name).toBe('Inicial');

    TestBed.resetTestingModule();
    const fresh = setup(CasesListStore, {
      list,
      preference: () => fail('platform.preference_not_found'),
    });
    await fresh.init();
    expect(fresh.columns()).toEqual(DEFAULT_COLUMNS);
    expect(fresh.saved()).toEqual([]);
  });

  it('keeps only the answer of the latest search and loads more by the cursor', async () => {
    let release: (value: ApiResult<CasesPageResponse>) => void = () => undefined;
    const slow = new Promise<ApiResult<CasesPageResponse>>((resolve) => (release = resolve));
    const list = vi
      .fn()
      .mockReturnValueOnce(slow)
      .mockReturnValueOnce(ok(page([summary('2')], 'c2')))
      .mockReturnValueOnce(ok(page([summary('3')])));
    const store = setup(CasesListStore, { list });

    const first = store.search({ ...NO_FILTERS, q: 'a' });
    await store.search({ ...NO_FILTERS, q: 'ab' });
    release({ ok: true, value: page([summary('1')]) });
    await first;
    await store.loadMore();

    expect(store.items().map((c) => c.id)).toEqual(['2', '3']);
    expect(store.nextCursor()).toBeNull();
    expect(list).toHaveBeenLastCalledWith({ ...NO_FILTERS, q: 'ab' }, 'c2');
  });

  it('saves a named filter and the columns as the user preference', async () => {
    const savePreference = vi.fn((_key: string, value: unknown) =>
      ok({ key: PREFERENCE_KEY, value, updatedAt: '' }),
    );
    const store = setup(CasesListStore, { list: () => ok(page([])), savePreference });
    await store.search({ ...NO_FILTERS, status: 'suspended' });

    await store.saveFilter(' Suspensos ');
    await store.setColumns(['status', 'folder']);
    await store.removeFilter('Suspensos');

    expect(savePreference).toHaveBeenNthCalledWith(1, PREFERENCE_KEY, {
      columns: DEFAULT_COLUMNS,
      saved: [{ name: 'Suspensos', filters: { ...NO_FILTERS, status: 'suspended' } }],
    });
    expect(store.columns()).toEqual(['folder', 'status']);
    expect(store.saved()).toEqual([]);
  });

  it('runs a batch on the selected cases and reloads the list', async () => {
    const batch = vi.fn(() =>
      ok({ updated: 1, skipped: [{ caseId: '2', code: 'litigation.case_closed' }] }),
    );
    const list = vi.fn(() => ok(page([summary('1'), summary('2')])));
    const store = setup(CasesListStore, { list, batch });
    await store.search(NO_FILTERS);

    store.toggleAll();
    expect(store.allSelected()).toBe(true);
    store.toggle('1');
    store.toggle('1');
    await store.runBatch({ action: 'changePhase', phaseId: 'f1' });

    expect(batch).toHaveBeenCalledWith({
      caseIds: ['2', '1'],
      action: 'changePhase',
      fromUserId: null,
      toUserId: null,
      role: null,
      phaseId: 'f1',
      note: null,
    });
    expect(store.batchResult()?.skipped[0].code).toBe('litigation.case_closed');
    expect(store.selected()).toEqual([]);
    expect(list).toHaveBeenCalledTimes(2);
  });
});

describe('CaseEditorStore', () => {
  it('looks up the CNJ, finds people and keeps the field errors of a refused save', async () => {
    const store = setup(CaseEditorStore, {
      lookupCnj: () =>
        ok({
          formatted: '2238058-98.2025.8.26.8434',
          year: 2025,
          segment: 8,
          courtCode: '26',
          origin: '8434',
          court: null,
        }),
      searchPeople: () =>
        ok({
          items: [
            {
              id: 'p1',
              kind: 'company',
              name: 'Banco Alfa',
              alternateName: null,
              document: null,
              roles: [],
              score: null,
            },
          ],
          nextCursor: null,
        }),
      create: () =>
        Promise.resolve({
          ok: false,
          code: 'validation.failed',
          errors: { parties: [{ code: 'litigation.client_required' }] },
        }),
    });
    await store.load(null);

    expect((await store.lookupCnj('22380589820258268434'))?.year).toBe(2025);
    await store.searchPeople('b');
    expect(store.people()).toEqual([]);
    await store.searchPeople('banco');
    expect(store.people()[0].id).toBe('p1');
    expect(await store.save('judicial', {} as never)).toBeNull();
    expect(store.fieldErrors()?.['parties'][0].code).toBe('litigation.client_required');
  });

  it('updates an existing case instead of creating one', async () => {
    const update = vi.fn(() => ok(legalCase({ district: 'São Paulo' })));
    const store = setup(CaseEditorStore, { get: () => ok(legalCase()), update });
    await store.load('c1');

    const saved = await store.save('judicial', {} as never);

    expect(update).toHaveBeenCalledWith('c1', {});
    expect(saved?.district).toBe('São Paulo');
  });
});

describe('CaseDetailStore', () => {
  it('changes the status, keeps the error of a refused action and pages the timeline', async () => {
    const timeline = vi
      .fn()
      .mockReturnValueOnce(ok({ items: [entry('e1')], nextCursor: 'x' }))
      .mockReturnValueOnce(ok({ items: [entry('e2')], nextCursor: null }));
    const store = setup(CaseDetailStore, {
      get: () => ok(legalCase()),
      setStatus: () => ok(legalCase({ status: 'suspended' })),
      reopen: () => fail('litigation.case_not_closed'),
      timeline,
    });
    await store.load('c1');

    expect(await store.setStatus('suspended')).toBe(true);
    expect(store.legalCase()?.status).toBe('suspended');
    expect(await store.reopen()).toBe(false);
    expect(store.errorCode()).toBe('litigation.case_not_closed');

    await store.loadTimeline(['litigation.hearing']);
    await store.moreTimeline();
    await store.moreTimeline();
    expect(store.timeline().map((e) => e.id)).toEqual(['e1', 'e2']);
    expect(timeline).toHaveBeenLastCalledWith('c1', ['litigation.hearing'], 'x');
  });

  it('links, unlinks, deletes and saves the restriction lists', async () => {
    const get = vi.fn(() => ok(legalCase()));
    const store = setup(CaseDetailStore, {
      get,
      list: () => ok(page([summary('c1'), summary('c2')])),
      link: () =>
        ok(
          legalCase({
            children: [
              {
                relationId: 'r1',
                caseId: 'c2',
                internalFolder: 'X',
                cnjNumber: null,
                kind: 'appeal',
              },
            ],
          }),
        ),
      unlink: () => ok(undefined),
      remove: () => ok(undefined),
      restrictions: () => ok({ allowed: [], blocked: [] }),
      setRestrictions: () => ok({ allowed: [{ userId: 'u1', teamId: null }], blocked: [] }),
    });
    await store.load('c1');

    await store.findCandidates('c');
    expect(store.candidates()).toEqual([]);
    await store.findCandidates('civ');
    expect(store.candidates().map((c) => c.id)).toEqual(['c2']);
    expect(await store.link('c2', 'appeal')).toBe(true);
    expect(store.legalCase()?.children.length).toBe(1);
    expect(await store.unlink('r1')).toBe(true);
    await store.loadRestrictions();
    expect(await store.saveRestrictions([{ userId: 'u1', teamId: null }], [])).toBe(true);
    expect(store.restrictions()?.allowed.length).toBe(1);
    expect(await store.remove()).toBe(true);
    expect(store.done()).toBe('deleted');
  });
});

describe('NumberingStore', () => {
  it('loads and saves the numbering format', async () => {
    const store = setup(NumberingStore, {
      numbering: () =>
        ok({ format: '{AREA}-{ANO}-{SEQ:00000}', areaCodes: {}, example: 'CIV-2026-00001' }),
      configureNumbering: (format: string) =>
        ok({ format, areaCodes: { a1: 'CIV' }, example: 'CIV-00001' }),
    });
    await store.load();

    expect(await store.save('{AREA}-{SEQ:00000}', { a1: 'CIV' })).toBe(true);
    expect(store.saved()).toBe(true);
    expect(store.numbering()?.example).toBe('CIV-00001');
  });
});
