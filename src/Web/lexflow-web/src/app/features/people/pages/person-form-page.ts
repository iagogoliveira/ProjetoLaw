import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  input,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import type {
  AddressInput,
  ContactChannel,
  ContactInput,
  PersonInput,
  PersonKind,
  PersonResponse,
  PersonRoleKind,
} from '../../../api/types.gen';
import {
  applyServerErrors,
  customFieldValues,
  CustomFields,
} from '../../../shared/ui/custom-fields/custom-fields';
import { BRAZILIAN_STATES, digitsOf, formatDocument, PERSON_ROLES } from '../data/people-format';
import { PersonEditorStore } from '../data/people-store';

const CHANNELS: readonly ContactChannel[] = ['email', 'phone', 'whatsApp'];

/** Espera entre a última tecla no nome ou no documento e a busca de possíveis duplicatas. */
const SIMILAR_DELAY_MS = 400;

type AddressForm = FormGroup<{
  street: FormControl<string>;
  number: FormControl<string>;
  complement: FormControl<string>;
  district: FormControl<string>;
  city: FormControl<string>;
  state: FormControl<string>;
  postalCode: FormControl<string>;
  isBilling: FormControl<boolean>;
}>;

type ContactForm = FormGroup<{
  name: FormControl<string>;
  department: FormControl<string>;
  email: FormControl<string>;
  phone: FormControl<string>;
  preferredChannel: FormControl<ContactChannel>;
}>;

const orNull = (value: string): string | null => (value.trim() === '' ? null : value.trim());

/**
 * Cadastro e edição de pessoa (RF-06): física ou jurídica (o tipo não muda depois), papéis, documento (com consulta
 * de CNPJ), contatos, endereços, dados fiscais, grupo econômico e campos customizados. Enquanto nome e documento são
 * digitados, mostra possíveis duplicatas; o servidor recusa documento repetido no escritório.
 */
@Component({
  selector: 'lf-person-form-page',
  imports: [
    CustomFields,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatSelectModule,
    ReactiveFormsModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [PersonEditorStore],
  templateUrl: './person-form-page.html',
  styleUrl: './people-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonFormPage implements OnInit, OnDestroy {
  /** Id da rota (`pessoas/:id/editar`); vazio no cadastro novo. */
  readonly id = input<string>();

  protected readonly store = inject(PersonEditorStore);
  protected readonly roles = PERSON_ROLES;
  protected readonly states = BRAZILIAN_STATES;
  protected readonly channels = CHANNELS;
  protected readonly format = formatDocument;
  protected readonly editing = computed(() => !!this.id());
  protected readonly submitted = signal(false);
  protected readonly cnpjFilled = signal(false);
  protected readonly newGroup = new FormControl('', { nonNullable: true });

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly subscriptions = new Subscription();
  private similarTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly form = this.fb.group({
    kind: this.fb.control<PersonKind>('company'),
    name: this.fb.control('', [Validators.required, Validators.maxLength(300)]),
    alternateName: this.fb.control('', Validators.maxLength(300)),
    document: this.fb.control(''),
    email: this.fb.control('', Validators.email),
    phone: this.fb.control(''),
    notes: this.fb.control('', Validators.maxLength(4000)),
    economicGroupId: this.fb.control<string | null>(null),
    roles: this.fb.group(
      Object.fromEntries(PERSON_ROLES.map((r) => [r, this.fb.control(false)])) as Record<
        PersonRoleKind,
        FormControl<boolean>
      >,
    ),
    fiscal: this.fb.group({
      stateRegistration: this.fb.control(''),
      municipalRegistration: this.fb.control(''),
      withholdsIss: this.fb.control(false),
      withholdsIncomeTax: this.fb.control(false),
      withholdsPisCofinsCsll: this.fb.control(false),
    }),
    addresses: this.fb.array<AddressForm>([]),
    contacts: this.fb.array<ContactForm>([]),
  });

  /** Campos customizados de pessoas (o componente `lf-custom-fields` cria os controles). */
  protected readonly customFields = new FormGroup({});

  protected get addresses(): FormArray<AddressForm> {
    return this.form.controls.addresses;
  }

  protected get contacts(): FormArray<ContactForm> {
    return this.form.controls.contacts;
  }

  protected get noRole(): boolean {
    return !PERSON_ROLES.some((r) => this.form.controls.roles.controls[r].value);
  }

  async ngOnInit(): Promise<void> {
    // Antes do carregamento: quem digita enquanto as listas chegam também recebe o aviso de duplicatas.
    this.subscriptions.add(
      this.form.controls.name.valueChanges.subscribe(() => this.scheduleSimilar()),
    );
    this.subscriptions.add(
      this.form.controls.document.valueChanges.subscribe(() => this.scheduleSimilar()),
    );

    await this.store.load(this.id() ?? null);
    const person = this.store.person();
    if (person) {
      this.fill(person);
    }
  }

  ngOnDestroy(): void {
    clearTimeout(this.similarTimer);
    this.subscriptions.unsubscribe();
  }

  protected addAddress(value?: Partial<AddressInput>): void {
    this.addresses.push(
      this.fb.group({
        street: this.fb.control(value?.street ?? '', Validators.required),
        number: this.fb.control(value?.number ?? ''),
        complement: this.fb.control(value?.complement ?? ''),
        district: this.fb.control(value?.district ?? ''),
        city: this.fb.control(value?.city ?? '', Validators.required),
        state: this.fb.control(value?.state ?? '', Validators.required),
        postalCode: this.fb.control(value?.postalCode ?? '', [
          Validators.required,
          Validators.pattern(/^\d{5}-?\d{3}$/),
        ]),
        isBilling: this.fb.control(value?.isBilling ?? false),
      }),
    );
  }

  protected addContact(value?: Partial<ContactInput>): void {
    this.contacts.push(
      this.fb.group({
        name: this.fb.control(value?.name ?? '', Validators.required),
        department: this.fb.control(value?.department ?? ''),
        email: this.fb.control(value?.email ?? '', Validators.email),
        phone: this.fb.control(value?.phone ?? ''),
        preferredChannel: this.fb.control<ContactChannel>(value?.preferredChannel ?? 'email'),
      }),
    );
  }

  /** Preenche o cadastro com os dados da Receita Federal (sem apagar o que já foi digitado). */
  protected async lookupCnpj(): Promise<void> {
    const company = await this.store.lookupCnpj(digitsOf(this.form.controls.document.value));
    if (!company) {
      return;
    }

    const c = this.form.controls;
    c.name.setValue(company.legalName);
    if (!c.alternateName.value && company.tradeName) {
      c.alternateName.setValue(company.tradeName);
    }
    if (!c.email.value && company.email) {
      c.email.setValue(company.email);
    }
    if (!c.phone.value && company.phone) {
      c.phone.setValue(company.phone);
    }
    if (company.address && this.addresses.length === 0) {
      this.addAddress(company.address);
    }
    this.cnpjFilled.set(true);
    this.changeDetector.markForCheck();
  }

  protected async createGroup(): Promise<void> {
    const name = this.newGroup.value.trim();
    if (!name) {
      return;
    }

    const group = await this.store.createGroup(name);
    if (group) {
      this.form.controls.economicGroupId.setValue(group.id);
      this.newGroup.setValue('');
    }
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    this.customFields.markAllAsTouched();
    if (this.form.invalid || this.customFields.invalid || this.noRole) {
      return;
    }

    const saved = await this.store.save(this.form.controls.kind.value, this.toInput());
    if (saved) {
      await this.router.navigate(['/pessoas', saved.id]);
      return;
    }

    applyServerErrors(this.customFields, this.store.fieldErrors());
    this.changeDetector.markForCheck();
  }

  private toInput(): PersonInput {
    const v = this.form.getRawValue();
    return {
      name: v.name.trim(),
      alternateName: orNull(v.alternateName),
      document: orNull(digitsOf(v.document)),
      email: orNull(v.email),
      phone: orNull(v.phone),
      notes: orNull(v.notes),
      fiscal: {
        stateRegistration: orNull(v.fiscal.stateRegistration),
        municipalRegistration: orNull(v.fiscal.municipalRegistration),
        withholdsIss: v.fiscal.withholdsIss,
        withholdsIncomeTax: v.fiscal.withholdsIncomeTax,
        withholdsPisCofinsCsll: v.fiscal.withholdsPisCofinsCsll,
      },
      economicGroupId: v.economicGroupId,
      roles: PERSON_ROLES.filter((r) => v.roles[r]),
      addresses: v.addresses.map((a) => ({
        street: a.street.trim(),
        number: orNull(a.number),
        complement: orNull(a.complement),
        district: orNull(a.district),
        city: a.city.trim(),
        state: a.state,
        postalCode: digitsOf(a.postalCode),
        isBilling: a.isBilling,
      })),
      contacts: v.contacts.map((c) => ({
        name: c.name.trim(),
        department: orNull(c.department),
        email: orNull(c.email),
        phone: orNull(c.phone),
        preferredChannel: c.preferredChannel,
      })),
      customFields: customFieldValues(this.customFields, this.store.fields()),
    };
  }

  private fill(person: PersonResponse): void {
    this.form.patchValue({
      kind: person.kind,
      name: person.name,
      alternateName: person.alternateName ?? '',
      document: formatDocument(person.document),
      email: person.email ?? '',
      phone: person.phone ?? '',
      notes: person.notes ?? '',
      economicGroupId: person.economicGroupId,
      roles: Object.fromEntries(PERSON_ROLES.map((r) => [r, person.roles.includes(r)])),
      fiscal: {
        stateRegistration: person.fiscal.stateRegistration ?? '',
        municipalRegistration: person.fiscal.municipalRegistration ?? '',
        withholdsIss: !!person.fiscal.withholdsIss,
        withholdsIncomeTax: !!person.fiscal.withholdsIncomeTax,
        withholdsPisCofinsCsll: !!person.fiscal.withholdsPisCofinsCsll,
      },
    });
    this.form.controls.kind.disable();
    person.addresses.forEach((a) => this.addAddress(a));
    person.contacts.forEach((c) => this.addContact(c));
    for (const [key, value] of Object.entries(person.customFields)) {
      this.customFields.addControl(key, new FormControl(value));
    }
    this.changeDetector.markForCheck();
  }

  private scheduleSimilar(): void {
    clearTimeout(this.similarTimer);
    this.similarTimer = setTimeout(
      () =>
        void this.store.findSimilar(
          this.form.controls.name.value,
          digitsOf(this.form.controls.document.value),
        ),
      SIMILAR_DELAY_MS,
    );
  }
}
