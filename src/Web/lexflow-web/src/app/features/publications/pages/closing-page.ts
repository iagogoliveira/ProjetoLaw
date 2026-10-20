import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { PublicationErrors } from '../components/publication-errors';
import { ClosingStore } from '../data/closing-store';
import { legalInstant, legalToday } from '../data/publications-format';

@Component({
  selector: 'lf-closing-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslocoDirective,
    CanDirective,
    PublicationErrors,
  ],
  providers: [ClosingStore],
  templateUrl: './closing-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClosingPage implements OnInit {
  protected readonly store = inject(ClosingStore);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly today = legalToday();
  protected readonly day = this.fb.control(this.today, Validators.required);
  protected readonly confirmed = this.fb.control(false, Validators.requiredTrue);
  protected readonly instant = legalInstant;
  ngOnInit(): void {
    void this.load();
  }
  protected async load(): Promise<void> {
    this.confirmed.reset(false);
    if (this.day.valid) await this.store.load(this.day.value);
  }
  protected async close(): Promise<void> {
    if (!this.confirmed.valid || this.day.value !== this.store.day()) return;
    if (await this.store.close()) this.confirmed.reset(false);
  }
}
