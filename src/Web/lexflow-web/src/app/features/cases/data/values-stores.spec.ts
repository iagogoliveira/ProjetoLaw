import { TestBed } from '@angular/core/testing';
import type {
  CaseClaimsResponse,
  CaseImportResponse,
  ClaimInput,
  HearingResponse,
  SettlementResponse,
} from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { CasesApi } from './cases-api';
import { ClaimsStore } from './claims-store';
import { HearingsStore } from './hearings-store';
import { ImportStore } from './import-store';
import { RiskStore } from './risk-store';
import { SettlementsStore } from './settlements-store';
import {
  editAmount,
  generateInstallments,
  hearingInstant,
  localDateTime,
  parseAmount,
} from './values-format';

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string) => Promise.resolve<ApiResult<never>>({ ok: false, code });
function setup<T>(store: new () => T, api: Partial<CasesApi>): T {
  TestBed.configureTestingModule({ providers: [store, { provide: CasesApi, useValue: api }] });
  return TestBed.inject(store);
}
const claims: CaseClaimsResponse = { items: [], totals: [], updatedTo: null };
const imported = (status: CaseImportResponse['status']): CaseImportResponse => ({
  id: 'i1',
  fileName: 'cases.csv',
  status,
  totalRows: 3,
  validRows: 2,
  invalidRows: 1,
  importedRows: status === 'completed' ? 2 : 0,
  preview: [],
  errors: [],
  errorCode: null,
  validationJobId: 'j1',
  importJobId: null,
  createdAt: '',
  completedAt: null,
});
const settlement = (status: SettlementResponse['status'] = 'negotiating'): SettlementResponse => ({
  id: 's1',
  caseId: 'c1',
  status,
  direction: 'clientPays',
  notes: null,
  agreedAmount: null,
  offers: [],
  installments: [],
  requestedAmount: 100,
  difference: null,
  differencePercent: null,
  submittedBy: null,
  submittedAt: null,
  approvedBy: null,
  approvedAt: null,
  homologatedOn: null,
  reason: null,
  createdAt: '',
});
const hearing = (id: string, scheduledAt: string): HearingResponse => ({
  id,
  caseId: 'c1',
  type: 'instruction',
  format: 'inPerson',
  status: 'scheduled',
  scheduledAt,
  location: 'Forum',
  meetingLink: null,
  lawyerUserId: null,
  representative: null,
  witnessPersonIds: [],
  notes: null,
  outcome: null,
  reschedules: [],
  createdAt: '',
});

describe('ClaimsStore', () => {
  it('ignores an old history response when a save starts and blocks another history read until it finishes', async () => {
    let releaseHistory!: (value: ApiResult<never[]>) => void;
    let releaseSave!: (value: ApiResult<never>) => void;
    const claimHistory = vi.fn(
      () => new Promise<ApiResult<never[]>>((resolve) => (releaseHistory = resolve)),
    );
    const store = setup(ClaimsStore, {
      claims: () => ok(claims),
      claimHistory,
      saveClaim: () => new Promise<ApiResult<never>>((resolve) => (releaseSave = resolve)),
    });
    await store.load('c1', null);
    const reading = store.showHistory('q1');
    const saving = store.save('q1', {} as never);
    await store.showHistory('q1');
    expect(claimHistory).toHaveBeenCalledOnce();
    releaseHistory({ ok: true, value: [] });
    await reading;
    releaseSave({ ok: true, value: {} as never });
    await saving;
    expect(store.historyClaimId()).toBeNull();
  });
  it('keeps only the most recent correction date response', async () => {
    let release!: (result: ApiResult<CaseClaimsResponse>) => void;
    const slow = new Promise<ApiResult<CaseClaimsResponse>>((resolve) => (release = resolve));
    const api = vi
      .fn()
      .mockReturnValueOnce(slow)
      .mockReturnValueOnce(ok({ ...claims, updatedTo: '2026-10-19' }));
    const store = setup(ClaimsStore, { claims: api });
    const first = store.load('c1', '2026-09-01');
    await store.load('c1', '2026-10-19');
    release({ ok: true, value: claims });
    await first;
    expect(store.data()?.updatedTo).toBe('2026-10-19');
    expect(store.loading()).toBe(false);
  });
  it('reloads corrected totals after saving and clears a deleted claim history', async () => {
    const list = vi.fn(() => ok(claims));
    const saveClaim = vi.fn(() => ok({ id: 'q1' } as never));
    const store = setup(ClaimsStore, {
      claims: list,
      saveClaim,
      claimHistory: () => ok([]),
      deleteClaim: () => ok(undefined),
    });
    await store.load('c1', '2026-10-19');
    const input: ClaimInput = {
      claimTypeId: 't1',
      description: null,
      correction: null,
      valuations: [],
    };
    expect(await store.save(null, input)).toBe(true);
    expect(saveClaim).toHaveBeenCalledWith('c1', null, input);
    await store.showHistory('q1');
    expect(store.historyClaimId()).toBe('q1');
    await store.remove('q1');
    expect(store.historyClaimId()).toBeNull();
    expect(list).toHaveBeenLastCalledWith('c1', '2026-10-19');
    expect(list).toHaveBeenCalledTimes(3);
  });
  it('preserves validation details and existing data after a refused save', async () => {
    const errors = { claimTypeId: [{ code: 'platform.lookup_item_not_found' }] };
    const store = setup(ClaimsStore, {
      claims: () => ok(claims),
      saveClaim: () => Promise.resolve({ ok: false, code: 'validation.failed', errors }),
    });
    await store.load('c1', null);
    expect(await store.save('q1', {} as never)).toBe(false);
    expect(store.data()).toEqual(claims);
    expect(store.fieldErrors()).toEqual(errors);
    expect(store.busy()).toBe(false);
  });
});

describe('RiskStore', () => {
  it('refreshes current accounting totals and history after assessing a claim', async () => {
    const risk = vi
      .fn()
      .mockReturnValueOnce(ok({ case: null, claims: [], provision: 0, contingentLiability: 0 }))
      .mockReturnValueOnce(ok({ case: null, claims: [], provision: 300, contingentLiability: 0 }));
    const assessRisk = vi.fn(() => ok({} as never));
    const store = setup(RiskStore, {
      risk,
      riskHistory: () => ok([]),
      claims: () => ok(claims),
      assessRisk,
    });
    await store.load('c1');
    const input = {
      claimId: 'q1',
      probability: 'probable' as const,
      estimatedLoss: 300,
      rationale: 'Evidence',
    };
    expect(await store.assess(input)).toBe(true);
    expect(assessRisk).toHaveBeenCalledWith('c1', input);
    expect(store.data()?.provision).toBe(300);
  });
  it('reports a denied assessment without replacing the previous totals', async () => {
    const store = setup(RiskStore, {
      risk: () => ok({ case: null, claims: [], provision: 100, contingentLiability: 50 }),
      riskHistory: () => ok([]),
      claims: () => ok(claims),
      assessRisk: () => fail('authorization.denied'),
    });
    await store.load('c1');
    expect(await store.assess({} as never)).toBe(false);
    expect(store.data()?.provision).toBe(100);
    expect(store.errorCode()).toBe('authorization.denied');
    expect(store.busy()).toBe(false);
  });
});

describe('SettlementsStore', () => {
  it('keeps the returned settlement through approval, homologation and payment', async () => {
    const step = vi
      .fn()
      .mockReturnValueOnce(ok(settlement('approved')))
      .mockReturnValueOnce(ok(settlement('homologated')));
    const pay = vi.fn(() => ok(settlement('fulfilled')));
    const store = setup(SettlementsStore, {
      settlements: () => ok([settlement('awaitingApproval')]),
      settlementStep: step,
      payInstallment: pay,
    });
    await store.load('c1');
    await store.step('s1', 'approve');
    expect(store.items()[0].status).toBe('approved');
    await store.step('s1', 'homologate', '2026-10-19');
    expect(step).toHaveBeenLastCalledWith('c1', 's1', 'homologate', '2026-10-19');
    await store.pay('s1', 'p1', { paidOn: '2026-10-19', paidAmount: 100 });
    expect(store.items()[0].status).toBe('fulfilled');
    expect(store.items()).toHaveLength(1);
  });
  it('keeps the negotiation when the server refuses inconsistent installments', async () => {
    const store = setup(SettlementsStore, {
      settlements: () => ok([settlement()]),
      submitSettlement: () => fail('litigation.settlement_installments_mismatch'),
    });
    await store.load('c1');
    expect(await store.submit('s1', { agreedAmount: 100, installments: [] })).toBe(false);
    expect(store.items()[0].status).toBe('negotiating');
    expect(store.errorCode()).toBe('litigation.settlement_installments_mismatch');
    expect(store.busy()).toBe(false);
  });
  it('adds offers and replaces terms returned to negotiation without duplicating the settlement', async () => {
    const offer = vi.fn(() =>
      ok({
        ...settlement(),
        offers: [
          { round: 1, amount: 100, party: 'office' as const, terms: null, offeredOn: '2026-10-19' },
        ],
      }),
    );
    const store = setup(SettlementsStore, {
      settlements: () => ok([]),
      startSettlement: () => ok(settlement()),
      offer,
      settlementStep: () => ok(settlement()),
      submitSettlement: () => ok(settlement('awaitingApproval')),
    });
    await store.load('c1');
    await store.start({ direction: 'clientPays', firstOffer: null, notes: null });
    await store.offer('s1', { party: 'office', amount: 100, terms: null, offeredOn: '2026-10-19' });
    expect(store.items()[0].offers).toHaveLength(1);
    await store.submit('s1', {
      agreedAmount: 100,
      installments: [{ amount: 100, dueOn: '2026-11-19' }],
    });
    await store.step('s1', 'return', 'Review terms');
    expect(store.items()).toHaveLength(1);
    expect(store.items()[0].status).toBe('negotiating');
  });
});

describe('HearingsStore', () => {
  it('sorts hearings by date and moves a rescheduled hearing without duplicating it', async () => {
    const a = hearing('a', '2026-10-20T12:00:00Z');
    const b = hearing('b', '2026-10-21T12:00:00Z');
    const store = setup(HearingsStore, {
      hearings: () => ok([b, a]),
      rescheduleHearing: () =>
        ok({
          ...a,
          scheduledAt: '2026-10-22T12:00:00Z',
          reschedules: [
            {
              previousDate: a.scheduledAt,
              newDate: '2026-10-22T12:00:00Z',
              reason: 'Court',
              changedBy: 'u1',
              changedAt: '',
            },
          ],
        }),
      endHearing: () => ok({ ...a, status: 'held', outcome: 'Minutes' }),
    });
    await store.load('c1');
    expect(store.items().map((h) => h.id)).toEqual(['a', 'b']);
    await store.reschedule('a', { newDate: '2026-10-22T12:00:00Z', reason: 'Court' });
    expect(store.items().map((h) => h.id)).toEqual(['b', 'a']);
    expect(store.items()[1].reschedules).toHaveLength(1);
    await store.end('a', 'hold', 'Minutes');
    expect(store.items().find((h) => h.id === 'a')?.outcome).toBe('Minutes');
  });
  it('keeps removed witnesses and ignores outdated people search responses', async () => {
    let release!: (v: ApiResult<never>) => void;
    const slow = new Promise<ApiResult<never>>((resolve) => (release = resolve));
    const searchPeople = vi
      .fn()
      .mockReturnValueOnce(slow)
      .mockReturnValueOnce(ok({ items: [{ id: 'p1', name: 'Ana' }], nextCursor: null }));
    const store = setup(HearingsStore, {
      hearings: () =>
        ok([{ ...hearing('a', '2026-10-20T12:00:00Z'), witnessPersonIds: ['removed'] }]),
      person: () => fail('crm.person_not_found'),
      searchPeople,
    });
    await store.load('c1');
    expect(store.witnessNames()['removed']).toBeNull();
    const first = store.searchPeople('An');
    await store.searchPeople('Ana');
    release({ ok: false, code: 'http.error' });
    await first;
    expect(store.people()[0].name).toBe('Ana');
    await store.searchPeople('a');
    expect(store.people()).toEqual([]);
  });
  it('keeps the hearing and validation fields when a format lacks a location', async () => {
    const fields = { location: [{ code: 'litigation.hearing_place_required' }] };
    const store = setup(HearingsStore, {
      hearings: () => ok([]),
      saveHearing: () => Promise.resolve({ ok: false, code: 'validation.failed', errors: fields }),
    });
    await store.load('c1');
    expect(await store.save(null, {} as never)).toBe(false);
    expect(store.fieldErrors()).toEqual(fields);
    expect(store.busy()).toBe(false);
  });
});

describe('ImportStore', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });
  it('polls validation and import until completion, confirming only ready valid rows', async () => {
    vi.useFakeTimers();
    const status = vi
      .fn()
      .mockReturnValueOnce(ok(imported('validating')))
      .mockReturnValueOnce(ok(imported('ready')))
      .mockReturnValueOnce(ok(imported('importing')))
      .mockReturnValueOnce(ok(imported('completed')));
    const confirm = vi.fn(() => ok({ importId: 'i1', jobId: 'j2' }));
    const store = setup(ImportStore, {
      startImport: () => ok({ importId: 'i1', jobId: 'j1' }),
      importStatus: status,
      confirmImport: confirm,
    });
    await store.upload(new File(['tipo;cliente_nome'], 'cases.csv'));
    expect(await store.confirm()).toBe(false);
    await vi.advanceTimersByTimeAsync(1500);
    expect(store.data()?.status).toBe('ready');
    await store.confirm();
    expect(confirm).toHaveBeenCalledOnce();
    await vi.advanceTimersByTimeAsync(1500);
    expect(store.data()?.importedRows).toBe(2);
    await vi.advanceTimersByTimeAsync(10000);
    expect(status).toHaveBeenCalledTimes(4);
  });
  it('stops polling after destroy and ignores an in-flight answer', async () => {
    vi.useFakeTimers();
    let release!: (v: ApiResult<CaseImportResponse>) => void;
    const status = vi.fn(
      () => new Promise<ApiResult<CaseImportResponse>>((resolve) => (release = resolve)),
    );
    const store = setup(ImportStore, { importStatus: status });
    const loading = store.resume('i1');
    TestBed.resetTestingModule();
    release({ ok: true, value: imported('validating') });
    await loading;
    await vi.advanceTimersByTimeAsync(10000);
    expect(status).toHaveBeenCalledOnce();
    expect(store.data()).toBeNull();
  });
  it('allows retrying a status read after a network error without uploading again', async () => {
    const status = vi
      .fn()
      .mockReturnValueOnce(fail('http.error'))
      .mockReturnValueOnce(ok(imported('completed')));
    const store = setup(ImportStore, { importStatus: status });
    await store.resume('i1');
    expect(store.errorCode()).toBe('http.error');
    await store.refresh();
    expect(store.data()?.status).toBe('completed');
    expect(store.errorCode()).toBeNull();
  });
  it('returns binary downloads and keeps download failures visible', async () => {
    const blob = new Blob(['xlsx'], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });
    const store = setup(ImportStore, {
      importTemplate: () => ok(blob),
      importStatus: () => ok(imported('ready')),
      importErrors: () => fail('http.error'),
    });
    expect(await store.download('template')).toBe(blob);
    await store.resume('i1');
    expect(await store.download('errors')).toBeNull();
    expect(store.errorCode()).toBe('http.error');
    expect(store.busy()).toBe(false);
  });
  it('does not confirm an empty preview or start polling after a refused upload', async () => {
    const confirm = vi.fn();
    const store = setup(ImportStore, {
      startImport: () => fail('litigation.import_file_invalid'),
      importStatus: () => ok({ ...imported('ready'), validRows: 0 }),
      confirmImport: confirm,
    });
    expect(await store.upload(new File([''], 'bad.csv'))).toBe(false);
    expect(store.errorCode()).toBe('litigation.import_file_invalid');
    await store.resume('i1');
    expect(await store.confirm()).toBe(false);
    expect(confirm).not.toHaveBeenCalled();
  });
});

describe('value form helpers', () => {
  it('splits cents exactly and anchors each due date to the original month day', () => {
    expect(generateInstallments(100, 3, '2026-01-31', 1)).toEqual([
      { amount: '33.33', dueOn: '2026-01-31' },
      { amount: '33.33', dueOn: '2026-02-28' },
      { amount: '33.34', dueOn: '2026-03-31' },
    ]);
    expect(generateInstallments(0.02, 3, '2026-01-31', 1)).toEqual([]);
    expect(generateInstallments(100, 2.5, '2026-01-31', 1)).toEqual([]);
    expect(generateInstallments(100, 361, '2026-01-31', 1)).toEqual([]);
    expect(generateInstallments(100, 1, '', 1)).toEqual([]);
    expect(generateInstallments(100, 2, '2026-12-31', 2)[1].dueOn).toBe('2027-02-28');
  });
  it('parses Brazilian amounts including zero and rejects invalid input', () => {
    expect(parseAmount('1.234,56')).toBe(1234.56);
    expect(parseAmount('0')).toBe(0);
    expect(parseAmount('1.23')).toBeNull();
    expect(parseAmount('abc')).toBeNull();
    expect(parseAmount('')).toBeNull();
    expect(editAmount(1234.56)).toBe('1234,56');
    expect(editAmount(null)).toBe('');
  });
  it('round trips hearing inputs in the legal timezone', () => {
    expect(hearingInstant('2026-10-19T09:00')).toBe('2026-10-19T12:00:00.000Z');
    expect(localDateTime('2026-10-19T12:00:00Z')).toBe('2026-10-19T09:00');
    expect(hearingInstant('2018-01-15T09:00')).toBe('2018-01-15T11:00:00.000Z');
    expect(localDateTime('2018-01-15T11:00:00Z')).toBe('2018-01-15T09:00');
  });
});
