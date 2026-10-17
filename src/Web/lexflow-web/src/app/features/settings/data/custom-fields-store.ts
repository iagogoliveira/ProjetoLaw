import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type {
  CreateCustomFieldRequest,
  CustomFieldResponse,
  LookupItemResponse,
  LookupTableResponse,
  MemberResponse,
} from '../../../api/types.gen';
import type { ApiResult, FieldErrors } from '../../../core/data/api-result';
import { AccessStore } from '../../../core/organization/access-store';
import type { CustomFieldUser } from '../../../shared/ui/custom-fields/custom-fields';
import { SettingsApi } from './settings-api';

/** Entidades que aceitam campos customizados (Platform.Contracts `CustomFieldEntities`). */
export const CUSTOM_FIELD_ENTITIES = [
  'cases',
  'people',
  'fee_agreements',
  'opportunities',
] as const;

/** Resultado do formulário de teste: válido, ou os erros por campo devolvidos pela API. */
export type TestResult = { valid: true } | { valid: false; errors: FieldErrors | undefined };

interface CustomFieldsState {
  entity: string;
  fields: CustomFieldResponse[];
  tables: LookupTableResponse[];
  users: CustomFieldUser[];
  test: TestResult | null;
  busy: boolean;
  errorCode: string | null;
}

const initialState: CustomFieldsState = {
  entity: 'cases',
  fields: [],
  tables: [],
  users: [],
  test: null,
  busy: false,
  errorCode: null,
};

const toUser = (m: MemberResponse): CustomFieldUser => ({
  userId: m.userId,
  name: m.fullName ?? m.email ?? m.userId,
});

/** Campos customizados por entidade, tabelas auxiliares e o formulário de teste (RF-03). */
export const CustomFieldsStore = signalStore(
  withState(initialState),
  withMethods((store, api = inject(SettingsApi), access = inject(AccessStore)) => {
    // Só a resposta da última validação conta (cliques seguidos não trocam o resultado por um antigo).
    let lastTest = 0;

    async function load(): Promise<void> {
      const [fields, tables] = await Promise.all([
        api.customFields(store.entity()),
        api.lookupTables(),
      ]);
      if (!fields.ok || !tables.ok) {
        patchState(store, {
          errorCode: !fields.ok ? fields.code : !tables.ok ? tables.code : null,
        });
        return;
      }

      patchState(store, { fields: fields.value, tables: tables.value });
      if (access.can('settings.users.read') && store.users().length === 0) {
        const members = await api.members();
        patchState(store, { users: members.ok ? members.value.map(toUser) : [] });
      }
    }

    async function change(call: () => Promise<ApiResult<unknown>>): Promise<boolean> {
      patchState(store, { busy: true, errorCode: null });
      const result = await call();
      patchState(store, { busy: false, errorCode: result.ok ? null : result.code });
      if (result.ok) {
        await load();
      }

      return result.ok;
    }

    return {
      load,

      async selectEntity(entity: string): Promise<void> {
        patchState(store, { entity, test: null });
        await load();
      },

      createField: (body: Omit<CreateCustomFieldRequest, 'entity'>) =>
        change(() => api.createCustomField({ ...body, entity: store.entity() })),

      setFieldState: (
        field: CustomFieldResponse,
        changes: { required?: boolean; isActive?: boolean },
      ) =>
        change(() =>
          api.updateCustomField(field.id, {
            label: field.label,
            helpText: field.helpText,
            required: changes.required ?? field.required,
            filterable: field.filterable,
            position: field.position,
            isActive: changes.isActive ?? field.isActive,
            settings: field.settings,
          }),
        ),

      createTable: (name: string) => change(() => api.createLookupTable(name)),

      addItem: (tableId: string, name: string) => change(() => api.createLookupItem(tableId, name)),

      setItemActive: (item: LookupItemResponse, isActive: boolean) =>
        change(() =>
          api.updateLookupItem(item.id, { name: item.name, position: item.position, isActive }),
        ),

      /**
       * Formulário de teste: a mesma validação que os módulos fazem antes de gravar. (O nome não pode ser `test`, que
       * é o estado: o método esconderia o signal.)
       */
      async validate(values: Record<string, unknown>): Promise<TestResult | null> {
        const current = ++lastTest;
        patchState(store, { busy: true });
        const result = await api.validateCustomFields(store.entity(), values);
        if (current !== lastTest) {
          return null;
        }

        const test: TestResult = result.ok
          ? { valid: true }
          : { valid: false, errors: result.errors };
        patchState(store, { busy: false, test });
        return test;
      },
    };
  }),
);
