import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { AccessStore } from '../../../core/organization/access-store';
import type { TaskDetailResponse, TaskPriority, TaskState } from '../../../api/types.gen';
import type { TaskFilters } from '../data/tasks-api';
import { TasksStore } from '../data/tasks-store';
import { TasksApi } from '../data/tasks-api';
import { taskErrorTranslationKey } from '../data/task-error-key';
import { formatDate } from '../../deadlines/data/deadline-format';

const STATES: TaskState[] = ['pending', 'inProgress', 'completed', 'cancelled'];
const PRIORITIES: TaskPriority[] = ['low', 'normal', 'high', 'urgent'];
function validGuid(value: string | null): string | null {
  return value && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
    ? value
    : null;
}
@Component({
  selector: 'lf-tasks-list-page',
  imports: [
    ReactiveFormsModule,
    CdkDrag,
    CdkDropList,
    CdkDropListGroup,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
    CanDirective,
  ],
  providers: [TasksStore],
  templateUrl: './tasks-list-page.html',
  styleUrls: ['../../../shared/ui/page.scss', './tasks-list-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TasksListPage {
  protected readonly errorTranslationKey = taskErrorTranslationKey;
  protected readonly store = inject(TasksStore);
  protected readonly states = STATES;
  protected readonly priorities = PRIORITIES;
  protected readonly date = formatDate;
  protected readonly isCaseSearchAllowed = inject(AccessStore).can('cases.read');
  protected readonly canManageTasks = inject(AccessStore).can('schedule.tasks.manage');
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  protected readonly filterForm = this.formBuilder.group({
    status: this.formBuilder.control<TaskState | null>(null),
    priority: this.formBuilder.control<TaskPriority | null>(null),
    assigneeUserId: this.formBuilder.control<string | null>(null),
    teamId: this.formBuilder.control<string | null>(null),
    caseId: this.formBuilder.control<string | null>(null),
  });
  protected readonly savedName = this.formBuilder.nonNullable.control('');
  protected readonly caseSearch = this.formBuilder.nonNullable.control('');
  protected readonly selectedIds = new Set<string>();
  protected readonly view = signal<'board' | 'list'>('board');
  protected readonly caseNames = signal(new Map<string, string>());
  private readonly api = inject(TasksApi);
  private readonly caseLookups = new Set<string>();
  private caseLookupVersion = 0;
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      const filters = this.readFilters(params.get.bind(params));
      this.view.set(params.get('view') === 'list' ? 'list' : 'board');
      this.filterForm.setValue(filters);
      this.selectedIds.clear();
      void this.store.search(filters);
      if (this.isCaseSearchAllowed && filters.caseId) void this.store.resolveCase(filters.caseId);
    });
    void this.store.loadOptions();
    if (inject(AccessStore).can('identity.profile.read')) void this.store.loadSaved();
    inject(DestroyRef).onDestroy(() => this.caseLookupVersion++);
    effect(() => {
      const visible = new Set(this.store.items().map((item) => item.id));
      for (const id of this.selectedIds) if (!visible.has(id)) this.selectedIds.delete(id);
    });
    effect(() => {
      if (!this.isCaseSearchAllowed) return;
      const ids = [
        ...new Set(
          this.store
            .items()
            .map((task) => task.caseId)
            .filter((id): id is string => id !== null),
        ),
      ];
      void this.resolveCaseNames(ids);
    });
  }
  protected apply(): void {
    void this.navigate(this.filterForm.getRawValue() as TaskFilters);
  }
  protected async navigate(filters: TaskFilters): Promise<void> {
    const query: Params = {
      status: filters.status,
      priority: filters.priority,
      assigneeUserId: filters.assigneeUserId,
      teamId: filters.teamId,
      caseId: filters.caseId,
      view: this.view() === 'list' ? 'list' : null,
    };
    const normalized = Object.fromEntries(
      Object.entries(query).map(([key, value]) => [key, value || null]),
    );
    const success = await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: normalized,
      replaceUrl: true,
    });
    if (!success) void this.store.search(filters);
  }
  protected saveFilter(): void {
    void this.store.saveFilter(this.savedName.getRawValue());
  }
  protected applySaved(name: string): void {
    const saved = this.store.saved().find((x) => x.name === name);
    if (saved) void this.navigate(saved.filters);
  }
  protected loadCases(query: string): void {
    if (this.isCaseSearchAllowed) void this.store.searchCases(query);
  }
  protected drop(event: CdkDragDrop<TaskState>, state: TaskState): void {
    if (!this.canManageTasks || this.store.busy()) return;
    if (event.previousContainer === event.container) return;
    const task = event.item.data as TaskDetailResponse;
    void this.store.changeState(task.id, state);
  }
  protected setView(view: 'board' | 'list'): void {
    this.view.set(view);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { view: view === 'list' ? 'list' : null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
  protected toggleSelected(id: string, checked: boolean): void {
    if (checked) this.selectedIds.add(id);
    else this.selectedIds.delete(id);
  }
  protected async redistribute(assigneeUserId: string | null): Promise<void> {
    if (
      !this.canManageTasks ||
      !assigneeUserId ||
      !this.selectedIds.size ||
      this.store.busy() ||
      this.store.loading()
    )
      return;
    await this.store.redistribute([...this.selectedIds], assigneeUserId);
    this.selectedIds.clear();
  }
  protected memberName(id: string): string {
    return this.store.options()?.members.find((x) => x.userId === id)?.name ?? '';
  }
  protected caseName(id: string): string {
    return this.caseNames().get(id) ?? '';
  }
  private async resolveCaseNames(ids: string[]): Promise<void> {
    const version = this.caseLookupVersion;
    const pending = ids.filter((id) => !this.caseLookups.has(id));
    if (!pending.length) return;
    pending.forEach((id) => this.caseLookups.add(id));
    const values = await Promise.all(
      pending.map(async (id) => {
        const result = await this.api.caseDetail(id);
        return result.ok
          ? ([id, result.value.cnjNumber ?? result.value.internalFolder] as const)
          : null;
      }),
    );
    if (version !== this.caseLookupVersion) return;
    const next = new Map(this.caseNames());
    for (const value of values) if (value) next.set(value[0], value[1]);
    this.caseNames.set(next);
  }
  private readFilters(get: (key: string) => string | null): TaskFilters {
    const status = get('status') as TaskState | null,
      priority = get('priority') as TaskPriority | null;
    return {
      status: status && STATES.includes(status) ? status : null,
      priority: priority && PRIORITIES.includes(priority) ? priority : null,
      assigneeUserId: validGuid(get('assigneeUserId')),
      teamId: validGuid(get('teamId')),
      caseId: validGuid(get('caseId')),
    };
  }
}
