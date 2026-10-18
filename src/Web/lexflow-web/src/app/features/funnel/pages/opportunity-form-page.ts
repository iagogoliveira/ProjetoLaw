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
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { OpportunityInput, PersonKind, PersonSummary } from '../../../api/types.gen';
import {
  applyServerErrors,
  customFieldValues,
  CustomFields,
} from '../../../shared/ui/custom-fields/custom-fields';
import { digitsOf, formatDocument } from '../../../shared/br-format';
import { OpportunityStore } from '../data/opportunity-store';

const orNull = (value: string): string | null => (value.trim() === '' ? null : value.trim());

/** Espera entre a última tecla e a busca de pessoas cadastradas. */
const SEARCH_DELAY_MS = 300;

/**
 * Cadastro e edição de oportunidade (RF-08): o interessado é um cadastro novo (dados soltos, decisão de 2026-10-18)
 * ou uma pessoa já cadastrada; origem, valor estimado, previsão de fechamento, responsável e campos customizados.
 */
@Component({
  selector: 'lf-opportunity-form-page',
  imports: [
    CustomFields,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatSelectModule,
    ReactiveFormsModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [OpportunityStore],
  template: `
    <section class="page" *transloco="let t">
      <h1>{{ t(editing() ? 'funnel.form.editTitle' : 'funnel.form.newTitle') }}</h1>

      <form [formGroup]="form" (ngSubmit)="save()" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>{{ t('funnel.form.title') }}</mat-label>
          <input matInput formControlName="title" required />
          <mat-error>{{ t('errors.crm.title_required') }}</mat-error>
        </mat-form-field>

        <fieldset>
          <legend>{{ t('funnel.form.prospect') }}</legend>
          <mat-radio-group
            class="inline"
            [value]="mode()"
            (change)="setMode($event.value)"
            [attr.aria-label]="t('funnel.form.prospect')"
          >
            <mat-radio-button value="new">{{ t('funnel.form.newProspect') }}</mat-radio-button>
            <mat-radio-button value="person">{{
              t('funnel.form.existingPerson')
            }}</mat-radio-button>
          </mat-radio-group>

          @if (mode() === 'person') {
            @if (chosen(); as person) {
              <p>
                {{ person.name }}
                @if (person.document) {
                  ({{ format(person.document) }})
                }
                <button mat-button type="button" (click)="chosen.set(null)">
                  {{ t('funnel.form.change') }}
                </button>
              </p>
            } @else {
              <mat-form-field appearance="outline">
                <mat-label>{{ t('funnel.form.searchPerson') }}</mat-label>
                <input matInput type="search" (input)="search($any($event.target).value)" />
              </mat-form-field>
              <ul>
                @for (person of store.people(); track person.id) {
                  <li>
                    <button mat-button type="button" (click)="choose(person)">
                      {{ person.name }}
                      @if (person.document) {
                        ({{ format(person.document) }})
                      }
                    </button>
                  </li>
                }
              </ul>
              @if (submitted() && !chosen()) {
                <p class="error" role="alert">{{ t('errors.crm.prospect_required') }}</p>
              }
            }
          } @else {
            <div formGroupName="prospect" class="form">
              <mat-radio-group
                formControlName="kind"
                class="inline"
                [attr.aria-label]="t('people.form.kind')"
              >
                <mat-radio-button value="company">{{ t('people.kinds.company') }}</mat-radio-button>
                <mat-radio-button value="individual">{{
                  t('people.kinds.individual')
                }}</mat-radio-button>
              </mat-radio-group>
              <mat-form-field appearance="outline">
                <mat-label>{{ t('funnel.form.prospectName') }}</mat-label>
                <input matInput formControlName="name" required />
                <mat-error>{{ t('errors.crm.name_required') }}</mat-error>
              </mat-form-field>
              <div class="inline">
                <mat-form-field appearance="outline">
                  <mat-label>{{ t('people.document') }}</mat-label>
                  <input matInput formControlName="document" inputmode="numeric" />
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>{{ t('people.form.email') }}</mat-label>
                  <input matInput type="email" formControlName="email" />
                  <mat-error>{{ t('errors.crm.email_invalid') }}</mat-error>
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>{{ t('people.form.phone') }}</mat-label>
                  <input matInput type="tel" formControlName="phone" />
                </mat-form-field>
              </div>
            </div>
          }
        </fieldset>

        <div class="inline">
          <mat-form-field appearance="outline">
            <mat-label>{{ t('funnel.form.origin') }}</mat-label>
            <mat-select formControlName="originId">
              <mat-option [value]="null">{{ t('funnel.form.noOrigin') }}</mat-option>
              @for (origin of store.origins(); track origin.id) {
                <mat-option [value]="origin.id">{{ origin.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ t('funnel.form.estimatedValue') }}</mat-label>
            <span matTextPrefix>R$&nbsp;</span>
            <input matInput type="number" min="0" step="0.01" formControlName="estimatedValue" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ t('funnel.form.expectedCloseDate') }}</mat-label>
            <input matInput type="date" formControlName="expectedCloseDate" />
          </mat-form-field>
          @if (store.members().length > 0) {
            <mat-form-field appearance="outline">
              <mat-label>{{ t('funnel.form.responsible') }}</mat-label>
              <mat-select formControlName="responsibleUserId">
                <mat-option [value]="null">{{ t('funnel.form.noResponsible') }}</mat-option>
                @for (member of store.members(); track member.userId) {
                  <mat-option [value]="member.userId">{{
                    member.fullName ?? member.email
                  }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          }
        </div>

        @if (store.fields().length > 0) {
          <fieldset>
            <legend>{{ t('people.form.customFields') }}</legend>
            <lf-custom-fields
              [definitions]="store.fields()"
              [group]="customFields"
              [lookupTables]="store.tables()"
            />
          </fieldset>
        }

        <mat-form-field appearance="outline">
          <mat-label>{{ t('people.form.notes') }}</mat-label>
          <textarea matInput formControlName="notes" rows="3"></textarea>
        </mat-form-field>

        @if (store.errorCode(); as code) {
          <p class="error" role="alert">{{ t('errors.' + code) }}</p>
        }

        <div class="actions">
          <button mat-flat-button type="submit" [disabled]="store.busy()">
            {{ t('people.form.save') }}
          </button>
          <a mat-button [routerLink]="editing() ? ['/funil', id()] : ['/funil']">{{
            t('people.form.cancel')
          }}</a>
        </div>
      </form>
    </section>
  `,
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OpportunityFormPage implements OnInit, OnDestroy {
  readonly id = input<string>();

  protected readonly store = inject(OpportunityStore);
  protected readonly format = formatDocument;
  protected readonly editing = computed(() => !!this.id());
  protected readonly mode = signal<'new' | 'person'>('new');
  protected readonly chosen = signal<{ id: string; name: string; document: string | null } | null>(
    null,
  );
  protected readonly submitted = signal(false);
  protected readonly customFields = new FormGroup({});

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private timer: ReturnType<typeof setTimeout> | undefined;

  protected readonly form = this.fb.group({
    title: this.fb.control('', [Validators.required, Validators.maxLength(200)]),
    prospect: this.fb.group({
      kind: this.fb.control<PersonKind>('company'),
      name: this.fb.control('', [Validators.required, Validators.maxLength(300)]),
      document: this.fb.control(''),
      email: this.fb.control('', Validators.email),
      phone: this.fb.control(''),
    }),
    originId: this.fb.control<string | null>(null),
    estimatedValue: this.fb.control<number | null>(null, Validators.min(0)),
    expectedCloseDate: this.fb.control(''),
    responsibleUserId: this.fb.control<string | null>(null),
    notes: this.fb.control('', Validators.maxLength(4000)),
  });

  async ngOnInit(): Promise<void> {
    await this.store.load(this.id() ?? null);
    const opportunity = this.store.opportunity();
    if (!opportunity) {
      return;
    }

    this.form.patchValue({
      title: opportunity.title,
      prospect: {
        kind: opportunity.prospect.kind,
        name: opportunity.prospect.name,
        document: formatDocument(opportunity.prospect.document),
        email: opportunity.prospect.email ?? '',
        phone: opportunity.prospect.phone ?? '',
      },
      originId: opportunity.originId,
      estimatedValue:
        opportunity.estimatedValue === null ? null : Number(opportunity.estimatedValue),
      expectedCloseDate: opportunity.expectedCloseDate ?? '',
      responsibleUserId: opportunity.responsibleUserId,
      notes: opportunity.notes ?? '',
    });
    if (opportunity.personId) {
      this.setMode('person');
      this.chosen.set({
        id: opportunity.personId,
        name: opportunity.prospect.name,
        document: opportunity.prospect.document,
      });
    }
    for (const [key, value] of Object.entries(opportunity.customFields)) {
      this.customFields.addControl(key, new FormControl(value));
    }
    this.changeDetector.markForCheck();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  /** Pessoa cadastrada: os dados soltos não valem (nem bloqueiam o envio). */
  protected setMode(mode: 'new' | 'person'): void {
    this.mode.set(mode);
    const prospect = this.form.controls.prospect;
    if (mode === 'person') {
      prospect.disable();
    } else {
      prospect.enable();
      this.chosen.set(null);
    }
  }

  protected search(term: string): void {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.store.searchPeople(term), SEARCH_DELAY_MS);
  }

  protected choose(person: PersonSummary): void {
    this.chosen.set({ id: person.id, name: person.name, document: person.document });
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    this.customFields.markAllAsTouched();
    if (
      this.form.invalid ||
      this.customFields.invalid ||
      (this.mode() === 'person' && !this.chosen())
    ) {
      return;
    }

    const saved = await this.store.save(this.toInput());
    if (saved) {
      await this.router.navigate(['/funil', saved.id]);
      return;
    }

    applyServerErrors(this.customFields, this.store.fieldErrors());
    this.changeDetector.markForCheck();
  }

  private toInput(): OpportunityInput {
    const v = this.form.getRawValue();
    const person = this.mode() === 'person' ? this.chosen() : null;
    return {
      title: v.title.trim(),
      personId: person?.id ?? null,
      prospect: person
        ? null
        : {
            kind: v.prospect.kind,
            name: v.prospect.name.trim(),
            document: orNull(digitsOf(v.prospect.document)),
            email: orNull(v.prospect.email),
            phone: orNull(v.prospect.phone),
          },
      originId: v.originId,
      estimatedValue: v.estimatedValue,
      expectedCloseDate: orNull(v.expectedCloseDate),
      responsibleUserId: v.responsibleUserId,
      notes: orNull(v.notes),
      customFields: customFieldValues(this.customFields, this.store.fields()),
    };
  }
}
