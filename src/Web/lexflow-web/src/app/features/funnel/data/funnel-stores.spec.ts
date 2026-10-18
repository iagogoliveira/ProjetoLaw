import { TestBed } from '@angular/core/testing';
import type {
  ConflictCheckResponse,
  OpportunityResponse,
  OpportunityStage,
  OpportunitySummary,
  ProposalResponse,
} from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import { FunnelApi } from './funnel-api';
import { FunnelStore } from './funnel-store';
import { OpportunityStore } from './opportunity-store';
import { ProposalStore } from './proposal-store';
import { TemplatesStore } from './templates-store';

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string): Promise<ApiResult<never>> => Promise.resolve({ ok: false, code });

const card = (id: string, stage: OpportunityStage): OpportunitySummary => ({
  id,
  title: `Oportunidade ${id}`,
  prospectName: 'Construtora Horizonte',
  personId: null,
  stage,
  estimatedValue: 1000,
  expectedCloseDate: null,
  responsibleUserId: null,
  originId: null,
  stageChangedAt: '2026-10-18T12:00:00Z',
});

const opportunity = (overrides: Partial<OpportunityResponse> = {}): OpportunityResponse => ({
  id: 'o1',
  title: 'Contencioso trabalhista',
  personId: null,
  prospect: {
    kind: 'company',
    name: 'Construtora Horizonte',
    document: null,
    email: null,
    phone: null,
  },
  originId: null,
  estimatedValue: 60000,
  expectedCloseDate: null,
  responsibleUserId: null,
  notes: null,
  stage: 'lead',
  stageChangedAt: '2026-10-18T12:00:00Z',
  lossReasonId: null,
  lossNote: null,
  conflictCheckId: null,
  wonAt: null,
  customFields: {},
  createdAt: '2026-10-18T12:00:00Z',
  ...overrides,
});

const check = (overrides: Partial<ConflictCheckResponse> = {}): ConflictCheckResponse => ({
  id: 'c1',
  queryName: 'Construtora Horizonte',
  queryDocument: null,
  side: 'client',
  risk: 'none',
  proceeded: false,
  proceededAt: null,
  justification: null,
  checkedBy: 'u1',
  createdAt: '2026-10-18T12:00:00Z',
  hits: [],
  ...overrides,
});

const proposal = (overrides: Partial<ProposalResponse> = {}): ProposalResponse => ({
  id: 'p1',
  opportunityId: 'o1',
  title: 'Proposta 2026',
  scope: null,
  conditions: null,
  validUntil: null,
  templateId: null,
  currency: 'BRL',
  total: 42000,
  status: 'draft',
  items: [],
  createdBy: 'u1',
  submittedBy: null,
  submittedAt: null,
  approvedBy: null,
  approvedAt: null,
  returnNote: null,
  sentAt: null,
  acceptedAt: null,
  declinedAt: null,
  declineNote: null,
  createdAt: '2026-10-18T12:00:00Z',
  ...overrides,
});

function setup<T>(store: new () => T, api: Partial<FunnelApi>, permissions: string[] = []): T {
  TestBed.configureTestingModule({
    providers: [
      store,
      { provide: FunnelApi, useValue: api },
      { provide: AccessStore, useValue: { can: (p: string) => permissions.includes(p) } },
    ],
  });
  return TestBed.inject(store);
}

describe('FunnelStore', () => {
  const pages = (stage: OpportunityStage | null) =>
    ok({ items: stage === 'lead' ? [card('a', 'lead'), card('b', 'lead')] : [], nextCursor: null });

  it('loads one column per stage, the summary and, for approvers, the approval queue', async () => {
    const proposals = vi.fn(() =>
      ok({
        items: [
          {
            id: 'p1',
            opportunityId: 'a',
            prospectName: 'X',
            title: 'P',
            status: 'pendingApproval' as const,
            total: 1,
            createdAt: '',
            sentAt: null,
          },
        ],
        nextCursor: null,
      }),
    );
    const store = setup(
      FunnelStore,
      {
        opportunities: pages,
        summary: () => ok([{ stage: 'lead' as const, count: 2, estimatedValue: 2000 }]),
        proposals,
      },
      ['crm.proposals.approve'],
    );

    await store.load();

    expect(store.columns().lead.items.map((o) => o.id)).toEqual(['a', 'b']);
    expect(store.totals().get('lead')).toEqual({ count: 2, value: 2000 });
    expect(proposals).toHaveBeenCalledWith(null, 'pendingApproval');
    expect(store.approvals().length).toBe(1);
  });

  it('moves a card at once and puts it back when the server refuses', async () => {
    const move = vi
      .fn()
      .mockReturnValueOnce(ok(opportunity({ stage: 'meeting' })))
      .mockReturnValueOnce(fail('crm.opportunity_won'));
    const store = setup(FunnelStore, { opportunities: pages, summary: () => ok([]), move });
    await store.load();

    expect(await store.move('a', 'lead', 'meeting')).toBe(true);
    expect(store.columns().meeting.items.map((o) => o.id)).toEqual(['a']);
    expect(store.columns().meeting.items[0].stage).toBe('meeting');

    expect(await store.move('b', 'lead', 'proposal')).toBe(false);
    expect(store.columns().lead.items.map((o) => o.id)).toEqual(['b']);
    expect(store.columns().proposal.items).toEqual([]);
    expect(store.errorCode()).toBe('crm.opportunity_won');
  });

  it('does not drop cards into won or lost', async () => {
    const move = vi.fn();
    const store = setup(FunnelStore, { opportunities: pages, summary: () => ok([]), move });
    await store.load();

    expect(await store.move('a', 'lead', 'won')).toBe(false);
    expect(move).not.toHaveBeenCalled();
  });
});

describe('OpportunityStore', () => {
  const base: Partial<FunnelApi> = {
    customFields: () => ok([]),
    lookupTables: () =>
      ok([
        {
          id: 't1',
          name: 'Origens',
          systemKey: 'opportunity_origins',
          isActive: true,
          items: [
            { id: 'i2', tableId: 't1', name: 'Site', position: 2, isActive: true },
            { id: 'i1', tableId: 't1', name: 'Indicação', position: 1, isActive: true },
            { id: 'i3', tableId: 't1', name: 'Antiga', position: 3, isActive: false },
          ],
        },
      ]),
    proposals: () => ok({ items: [], nextCursor: null }),
  };

  it('offers only the active origins in order and creates the opportunity', async () => {
    const createOpportunity = vi.fn(() => ok(opportunity()));
    const store = setup(OpportunityStore, { ...base, createOpportunity });
    await store.load(null);

    expect(store.origins().map((o) => o.name)).toEqual(['Indicação', 'Site']);
    expect((await store.save({ title: 'T' } as never))?.id).toBe('o1');
  });

  it('links the conflict check to the opportunity and clears it after proceeding', async () => {
    const store = setup(
      OpportunityStore,
      {
        ...base,
        opportunity: () => ok(opportunity()),
        checkConflict: () => ok(check({ risk: 'indirect' })),
        proceedConflict: vi.fn(() => ok(check({ risk: 'indirect', proceeded: true }))),
      },
      ['crm.proposals.read'],
    );
    await store.load('o1');
    expect(store.conflict()).toBeNull();

    await store.checkConflict();
    expect(store.opportunity()?.conflictCheckId).toBe('c1');
    expect(store.conflictCleared()).toBe(false);

    expect(await store.proceedConflict('  Grupos distintos  ')).toBe(true);
    expect(store.conflictCleared()).toBe(true);
  });

  it('keeps the refusal when losing without a valid reason', async () => {
    const store = setup(OpportunityStore, {
      ...base,
      opportunity: () => ok(opportunity()),
      lose: () => fail('crm.loss_reason_invalid'),
    });
    await store.load('o1');

    expect(await store.lose('x', null)).toBe(false);
    expect(store.errorCode()).toBe('crm.loss_reason_invalid');
  });
});

describe('ProposalStore', () => {
  const base: Partial<FunnelApi> = {
    templates: () => ok({ templates: [], markers: [] }),
    opportunity: () => ok(opportunity()),
  };

  it('creates the draft in the opportunity of the route', async () => {
    const createProposal = vi.fn(() => ok(proposal()));
    const store = setup(ProposalStore, { ...base, createProposal });
    await store.load(null, 'o1');

    await store.save({ title: 'Proposta 2026' } as never, 'o1');

    expect(createProposal).toHaveBeenCalledWith('o1', { title: 'Proposta 2026' });
    expect(store.proposal()?.id).toBe('p1');
  });

  it('sends the workflow note trimmed and shows the refusal of self approval', async () => {
    const step = vi
      .fn()
      .mockReturnValueOnce(ok(proposal({ status: 'draft', returnNote: 'Rever' })))
      .mockReturnValueOnce(fail('crm.proposal_self_approval'));
    const store = setup(ProposalStore, { ...base, proposal: () => ok(proposal()), step }, [
      'crm.opportunities.read',
    ]);
    await store.load('p1', null);

    expect(await store.step('return', '  Rever  ')).toBe(true);
    expect(step).toHaveBeenCalledWith('p1', 'return', 'Rever');
    expect(await store.step('approve')).toBe(false);
    expect(store.errorCode()).toBe('crm.proposal_self_approval');
  });

  it('keeps the client created by the acceptance', async () => {
    const store = setup(ProposalStore, {
      ...base,
      proposal: () => ok(proposal({ status: 'sent' })),
      accept: () => ok({ proposal: proposal({ status: 'accepted' }), clientPersonId: 'person-1' }),
    });
    await store.load('p1', null);

    expect(await store.accept()).toBe(true);
    expect(store.proposal()?.status).toBe('accepted');
    expect(store.clientPersonId()).toBe('person-1');
  });
});

describe('TemplatesStore', () => {
  it('reloads the list after an upload and keeps the refusal code', async () => {
    const templates = vi
      .fn()
      .mockReturnValueOnce(ok({ templates: [], markers: ['cliente.nome'] }))
      .mockReturnValueOnce(
        ok({
          templates: [{ id: 't1', name: 'Silva', fileName: 'm.docx', size: 1024, createdAt: '' }],
          markers: ['cliente.nome'],
        }),
      );
    const uploadTemplate = vi
      .fn()
      .mockReturnValueOnce(
        ok({ id: 't1', name: 'Silva', fileName: 'm.docx', size: 1024, createdAt: '' }),
      )
      .mockReturnValueOnce(fail('crm.template_unknown_marker'));
    const store = setup(TemplatesStore, { templates, uploadTemplate });
    await store.load();
    const file = new File(['x'], 'm.docx');

    expect(await store.upload('Silva', file)).toBe(true);
    expect(store.templates().map((t) => t.name)).toEqual(['Silva']);
    expect(store.uploaded()).toBe(true);
    expect(await store.upload('Outro', file)).toBe(false);
    expect(store.errorCode()).toBe('crm.template_unknown_marker');
  });
});
