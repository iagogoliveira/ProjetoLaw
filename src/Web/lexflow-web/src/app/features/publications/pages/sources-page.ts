import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type {
  CaptureSourceKind,
  CaptureSourceResponse,
  MovementProviderKind,
} from '../../../api/types.gen';
import { CanDirective } from '../../../core/organization/can.directive';
import { PublicationErrors } from '../components/publication-errors';
import { legalInstant, legalToday } from '../data/publications-format';
import { SourcesStore } from '../data/sources-store';

@Component({
  selector: 'lf-publication-sources-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
    CanDirective,
    PublicationErrors,
  ],
  providers: [SourcesStore],
  templateUrl: './sources-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SourcesPage implements OnInit {
  protected readonly store = inject(SourcesStore);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly editing = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly today = legalToday();
  protected readonly captureDay = this.fb.control(this.today, Validators.required);
  protected readonly sourceForm = this.fb.group({
    kind: 'name' as CaptureSourceKind,
    label: ['', [Validators.required, Validators.maxLength(200)]],
    value: ['', Validators.required],
    isActive: true,
  });
  protected readonly integrationForm = this.fb.group({
    provider: 'disabled' as MovementProviderKind,
    apiKey: '',
    clearApiKey: false,
  });
  protected readonly kinds: CaptureSourceKind[] = ['name', 'oab', 'cnpj'];
  protected readonly providers: MovementProviderKind[] = ['disabled', 'fake', 'dataJud'];
  protected readonly instant = legalInstant;
  async ngOnInit(): Promise<void> {
    await this.store.load();
    this.integrationForm.patchValue({ provider: this.store.integration()?.provider ?? 'disabled' });
  }
  protected edit(source: CaptureSourceResponse | null): void {
    this.editing.set(source?.id ?? null);
    this.sourceForm.reset(source ?? { kind: 'name', label: '', value: '', isActive: true });
    this.saved.set(false);
  }
  protected async save(): Promise<void> {
    if (this.sourceForm.invalid) {
      this.sourceForm.markAllAsTouched();
      return;
    }
    if (await this.store.save(this.editing(), this.sourceForm.getRawValue())) {
      this.edit(null);
      this.saved.set(true);
    }
  }
  protected inspect(id: string): void {
    if (this.captureDay.valid) void this.store.inspect(id, this.captureDay.value);
  }
  protected canRetry(id: string): boolean {
    return this.store.items().some((s) => s.id === id && s.isActive);
  }
  protected async configure(): Promise<void> {
    const v = this.integrationForm.getRawValue();
    try {
      this.saved.set(await this.store.configure({ ...v, apiKey: v.apiKey || null }));
    } finally {
      this.integrationForm.patchValue({ apiKey: '', clearApiKey: false });
    }
  }
}
