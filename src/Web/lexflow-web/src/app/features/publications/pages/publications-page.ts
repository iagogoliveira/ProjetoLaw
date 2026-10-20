import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  HostListener,
  inject,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { PublicationStatus, PublicationTriageInput } from '../../../api/types.gen';
import { CanDirective } from '../../../core/organization/can.directive';
import { PublicationErrors } from '../components/publication-errors';
import { TriageForm } from '../components/triage-form';
import { PublicationsLiveStore } from '../data/publications-live-store';
import { PublicationsStore } from '../data/publications-store';
import { PublicationFilters } from '../data/publications-api';
import { legalInstant } from '../data/publications-format';
import { CaptureAlerts } from '../components/capture-alerts';

@Component({
  selector: 'lf-publications-page',
  imports: [
    CaptureAlerts,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
    CanDirective,
    PublicationErrors,
    TriageForm,
  ],
  providers: [PublicationsStore],
  templateUrl: './publications-page.html',
  styleUrls: ['../../../shared/ui/page.scss', './publications-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicationsPage implements OnInit {
  protected readonly store = inject(PublicationsStore);
  protected readonly live = inject(PublicationsLiveStore);
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly triageForm = viewChild(TriageForm);
  protected readonly shortcuts = this.fb.control(true);
  protected readonly filters = this.fb.group({ status: 'pending', linked: '', assignee: '' });
  protected readonly saved = signal(false);
  protected readonly instant = legalInstant;
  constructor() {
    effect(() => {
      if (this.live.revision() > 0) this.store.news();
    });
  }
  ngOnInit(): void {
    void this.store.loadOptions();
    let key = '';
    let version = 0;
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(async (params) => {
      const request = ++version;
      const status = params.get('status') ?? 'pending';
      const linked = params.get('linked');
      const filters: PublicationFilters = {
        status: status === 'triaged' ? 'triaged' : status === 'all' ? null : 'pending',
        linked: linked === 'true' ? true : linked === 'false' ? false : null,
        assignee: params.get('assignee'),
      };
      this.filters.setValue({
        status: filters.status ?? 'all',
        linked: filters.linked === null ? '' : String(filters.linked),
        assignee: filters.assignee ?? '',
      });
      const nextKey = JSON.stringify(filters);
      if (key !== nextKey) {
        key = nextKey;
        await this.store.search(filters);
      }
      if (request !== version) return;
      await this.store.select(params.get('publication'));
    });
  }
  protected applyFilters(): void {
    const f = this.filters.getRawValue();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { status: f.status, linked: f.linked || null, assignee: f.assignee || null },
    });
  }
  protected select(id: string): void {
    if (this.store.busy()) return;
    this.saved.set(false);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { publication: id },
      queryParamsHandling: 'merge',
    });
  }
  protected async refresh(): Promise<void> {
    await this.store.search(this.store.filters());
    await this.live.refreshCount();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { publication: null },
      queryParamsHandling: 'merge',
    });
  }
  protected changed(): void {
    if (this.destroyRef.destroyed) return;
    this.saved.set(true);
    void this.live.refreshCount();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { publication: this.store.selected()?.id ?? null },
      queryParamsHandling: 'merge',
    });
  }
  protected async triage(input: PublicationTriageInput): Promise<void> {
    const id = this.store.selected()?.id;
    if (id && (await this.store.triage(id, input)) && this.selectionUnchanged(id)) this.changed();
  }
  protected async link(caseId: string): Promise<void> {
    const id = this.store.selected()?.id;
    if (id && (await this.store.link(id, caseId)) && this.selectionUnchanged(id)) this.changed();
  }
  protected async assign(userId: string | null): Promise<void> {
    const id = this.store.selected()?.id;
    if (id && (await this.store.assign(id, userId)) && this.selectionUnchanged(id)) this.changed();
  }
  private selectionUnchanged(id: string): boolean {
    return (
      !this.destroyRef.destroyed && this.route.snapshot.queryParamMap.get('publication') === id
    );
  }
  protected fragments(text: string): { text: string; cnj: boolean }[] {
    return text
      .split(/(\d{7}-\d{2}\.\d{4}\.\d\.\d{2}\.\d{4}|\b\d{20}\b)/)
      .map((part, i) => ({ text: part, cnj: i % 2 === 1 }));
  }
  @HostListener('document:keydown', ['$event'])
  protected navigate(event: KeyboardEvent): void {
    if (
      !this.shortcuts.value ||
      event.defaultPrevented ||
      event.altKey ||
      event.ctrlKey ||
      event.metaKey ||
      event.shiftKey ||
      event.repeat ||
      this.store.busy()
    )
      return;
    const target = event.target;
    if (
      target instanceof HTMLElement &&
      target.closest(
        'input,textarea,select,[contenteditable="true"],[role="combobox"],[role="option"],[role="listbox"]',
      )
    )
      return;
    if (window.getSelection()?.toString()) return;
    const key = event.key.toLowerCase();
    if (key === 't' || key === 'p') {
      this.triageForm()?.focus(key === 'p');
      event.preventDefault();
      return;
    }
    if (key !== 'j' && key !== 'k') return;
    const items = this.store.items();
    const index = items.findIndex((p) => p.id === this.store.selected()?.id);
    const next = items[Math.max(0, Math.min(items.length - 1, index + (key === 'j' ? 1 : -1)))];
    if (next) {
      this.select(next.id);
      event.preventDefault();
    }
  }
  protected readonly statuses: PublicationStatus[] = ['pending', 'triaged'];
}
