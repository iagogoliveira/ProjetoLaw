import { TestBed } from '@angular/core/testing';
import type {
  PeoplePageResponse,
  PersonInput,
  PersonResponse,
  PersonSummary,
} from '../../../api/types.gen';
import type { ApiResult } from '../../../core/data/api-result';
import { CrmApi } from './crm-api';
import { digitsOf, formatDocument, formatPostalCode } from './people-format';
import { PeopleStore, PersonDetailStore, PersonEditorStore } from './people-store';

const ok = <T>(value: T): Promise<ApiResult<T>> => Promise.resolve({ ok: true, value });
const fail = (code: string, errors?: Record<string, { code: string }[]>) =>
  Promise.resolve<ApiResult<never>>({ ok: false, code, errors });

const summary = (id: string, name: string): PersonSummary => ({
  id,
  kind: 'company',
  name,
  alternateName: null,
  document: null,
  roles: ['client'],
  score: null,
});

const page = (items: PersonSummary[], nextCursor: string | null = null): PeoplePageResponse => ({
  items,
  nextCursor,
});

const person = (id = 'p1', overrides: Partial<PersonResponse> = {}): PersonResponse => ({
  id,
  kind: 'company',
  name: 'Horizonte S.A.',
  alternateName: null,
  document: '11222333000181',
  email: null,
  phone: null,
  notes: null,
  fiscal: {},
  economicGroupId: null,
  roles: ['client'],
  addresses: [],
  contacts: [],
  customFields: {},
  createdAt: '2026-10-18T12:00:00Z',
  anonymizedAt: null,
  ...overrides,
});

const input: PersonInput = {
  name: 'Horizonte S.A.',
  alternateName: null,
  document: '11222333000181',
  email: null,
  phone: null,
  notes: null,
  fiscal: null,
  economicGroupId: null,
  roles: ['client'],
  addresses: [],
  contacts: [],
  customFields: {},
};

function setup<T>(store: new () => T, api: Partial<CrmApi>): T {
  TestBed.configureTestingModule({
    providers: [
      store,
      {
        provide: CrmApi,
        useValue: {
          economicGroups: () => ok([]),
          customFields: () => ok([]),
          lookupTables: () => ok([]),
          similar: () => ok([]),
          ...api,
        },
      },
    ],
  });
  return TestBed.inject(store);
}

describe('people format', () => {
  it('formats CPF, CNPJ and CEP and keeps other values as typed', () => {
    expect(formatDocument('11222333000181')).toBe('11.222.333/0001-81');
    expect(formatDocument('52998224725')).toBe('529.982.247-25');
    expect(formatDocument('123')).toBe('123');
    expect(formatDocument(null)).toBe('');
    expect(formatPostalCode('01310100')).toBe('01310-100');
    expect(digitsOf('11.222.333/0001-81')).toBe('11222333000181');
  });
});

describe('PeopleStore', () => {
  it('keeps only the answer of the latest search', async () => {
    let releaseFirst: (value: ApiResult<PeoplePageResponse>) => void = () => undefined;
    const first = new Promise<ApiResult<PeoplePageResponse>>((resolve) => (releaseFirst = resolve));
    const people = vi
      .fn()
      .mockReturnValueOnce(first)
      .mockReturnValueOnce(ok(page([summary('p2', 'João da Silva Ltda.')])));
    const store = setup(PeopleStore, { people });

    const slow = store.search('jo', null);
    await store.search('joao da silva', null);
    releaseFirst({ ok: true, value: page([summary('p1', 'Outro')]) });
    await slow;

    expect(people).toHaveBeenLastCalledWith('joao da silva', null, null);
    expect(store.items().map((p) => p.name)).toEqual(['João da Silva Ltda.']);
  });

  it('appends the next page with the same term and role', async () => {
    const people = vi
      .fn()
      .mockReturnValueOnce(ok(page([summary('p1', 'A')], 'c1')))
      .mockReturnValueOnce(ok(page([summary('p2', 'B')])));
    const store = setup(PeopleStore, { people });

    await store.search('', 'opponent');
    await store.loadMore();

    expect(people).toHaveBeenLastCalledWith(null, 'opponent', 'c1');
    expect(store.items().map((p) => p.id)).toEqual(['p1', 'p2']);
    expect(store.nextCursor()).toBeNull();
  });
});

describe('PersonEditorStore', () => {
  it('creates a new person with the kind and updates an existing one', async () => {
    const createPerson = vi.fn(() => ok(person('new')));
    const updatePerson = vi.fn(() => ok(person('p1')));
    const creator = setup(PersonEditorStore, { createPerson });
    await creator.load(null);
    expect((await creator.save('individual', input))?.id).toBe('new');
    expect(createPerson).toHaveBeenCalledWith({ kind: 'individual', ...input });

    TestBed.resetTestingModule();
    const editor = setup(PersonEditorStore, { person: () => ok(person('p1')), updatePerson });
    await editor.load('p1');
    await editor.save('company', input);
    expect(updatePerson).toHaveBeenCalledWith('p1', input);
  });

  it('keeps the error code and the field errors when saving fails', async () => {
    const store = setup(PersonEditorStore, {
      createPerson: () => fail('crm.document_taken'),
    });
    await store.load(null);

    expect(await store.save('company', input)).toBeNull();
    expect(store.errorCode()).toBe('crm.document_taken');

    TestBed.resetTestingModule();
    const invalid = setup(PersonEditorStore, {
      createPerson: () =>
        fail('validation.failed', { 'customFields.origem': [{ code: 'custom_fields.required' }] }),
    });
    await invalid.load(null);
    await invalid.save('company', input);
    expect(invalid.fieldErrors()?.['customFields.origem'][0].code).toBe('custom_fields.required');
  });

  it('looks for duplicates only with enough to search and ignores the person being edited', async () => {
    const similar = vi.fn(() =>
      ok([
        {
          id: 'p2',
          kind: 'company' as const,
          name: 'Horizonte SA',
          document: null,
          sameDocument: false,
          score: 0.8,
        },
      ]),
    );
    const store = setup(PersonEditorStore, { person: () => ok(person('p1')), similar });
    await store.load('p1');

    await store.findSimilar('Ho', '');
    expect(similar).not.toHaveBeenCalled();
    await store.findSimilar('Horizonte', '');
    expect(similar).toHaveBeenCalledWith('Horizonte', null, 'p1');
    expect(store.similar().map((p) => p.id)).toEqual(['p2']);
  });

  it('adds a created economic group to the list', async () => {
    const store = setup(PersonEditorStore, {
      createEconomicGroup: () => ok({ id: 'g1', name: 'Grupo Horizonte', memberCount: 0 }),
    });
    await store.load(null);

    expect((await store.createGroup('Grupo Horizonte'))?.id).toBe('g1');
    expect(store.groups().map((g) => g.name)).toEqual(['Grupo Horizonte']);
  });
});

describe('PersonDetailStore', () => {
  it('merges a duplicate and reloads the remaining candidates', async () => {
    const similar = vi
      .fn()
      .mockReturnValueOnce(
        ok([
          {
            id: 'p2',
            kind: 'company',
            name: 'Horizonte SA',
            document: null,
            sameDocument: false,
            score: 0.9,
          },
        ]),
      )
      .mockReturnValueOnce(ok([]));
    const merge = vi.fn(() => ok(person('p1', { roles: ['client', 'opponent'] })));
    const store = setup(PersonDetailStore, { person: () => ok(person('p1')), similar, merge });
    await store.load('p1');
    expect(store.duplicates().length).toBe(1);

    expect(await store.merge('p2')).toBe(true);

    expect(merge).toHaveBeenCalledWith('p1', 'p2');
    expect(store.person()?.roles).toEqual(['client', 'opponent']);
    expect(store.duplicates()).toEqual([]);
    expect(store.done()).toBe('merged');
  });

  it('anonymizes with the reason and reports a refusal', async () => {
    const anonymize = vi
      .fn()
      .mockReturnValueOnce(
        ok(person('p1', { name: 'Pessoa anonimizada', anonymizedAt: '2026-10-18T12:00:00Z' })),
      )
      .mockReturnValueOnce(fail('crm.person_anonymized'));
    const store = setup(PersonDetailStore, { person: () => ok(person('p1')), anonymize });
    await store.load('p1');

    expect(await store.anonymize('Pedido do titular')).toBe(true);
    expect(anonymize).toHaveBeenCalledWith('p1', 'Pedido do titular');
    expect(store.person()?.anonymizedAt).not.toBeNull();
    expect(await store.anonymize('de novo')).toBe(false);
    expect(store.errorCode()).toBe('crm.person_anonymized');
  });
});
