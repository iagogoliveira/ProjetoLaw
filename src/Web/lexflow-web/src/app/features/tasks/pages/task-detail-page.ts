import {
  ChangeDetectionStrategy,
  computed,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { TaskInput, TaskPriority, TaskState } from '../../../api/types.gen';
import { TasksApi } from '../data/tasks-api';
import { TaskDetailStore } from '../data/task-detail-store';
import { AccessStore } from '../../../core/organization/access-store';
import { taskErrorTranslationKey } from '../data/task-error-key';
import { formatDate } from '../../deadlines/data/deadline-format';

const STATES: TaskState[] = ['pending', 'inProgress', 'completed', 'cancelled'];
const PRIORITIES: TaskPriority[] = ['low', 'normal', 'high', 'urgent'];
@Component({
  selector: 'lf-task-detail-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  providers: [TaskDetailStore],
  templateUrl: './task-detail-page.html',
  styleUrls: ['../../../shared/ui/page.scss', './task-detail-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskDetailPage {
  protected readonly errorTranslationKey = taskErrorTranslationKey;
  protected readonly store = inject(TaskDetailStore);
  protected readonly states = STATES;
  protected readonly priorities = PRIORITIES;
  protected readonly date = formatDate;
  protected commentDate(value: string): string {
    return new Intl.DateTimeFormat('pt-BR', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: 'America/Sao_Paulo',
    }).format(new Date(value));
  }
  protected readonly canManageTasks = inject(AccessStore).can('schedule.tasks.manage');
  protected readonly contentEditable = computed(
    () =>
      this.canManageTasks &&
      !!this.store.task() &&
      !['completed', 'cancelled'].includes(this.store.task()!.status),
  );
  protected readonly memberNames = signal(new Map<string, string>());
  protected readonly caseName = signal('');
  protected readonly canReadCases = inject(AccessStore).can('cases.read');
  protected readonly checklistForm = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
  });
  protected readonly commentForm = inject(FormBuilder).nonNullable.group({
    text: ['', [Validators.required, Validators.maxLength(4000)]],
  });
  protected readonly subtaskForm = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    dueOn: ['', Validators.required],
    assigneeUserId: ['', Validators.required],
    priority: ['normal' as TaskPriority, Validators.required],
    description: ['', Validators.maxLength(4000)],
  });
  protected readonly members = signal<{ userId: string; name: string }[]>([]);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(TasksApi);
  private readonly currentId = signal<string | null>(null);
  private caseVersion = 0;
  private alive = true;
  constructor() {
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(() => {
      this.alive = false;
      this.caseVersion++;
    });
    this.route.paramMap.pipe(takeUntilDestroyed(destroyRef)).subscribe((params) => {
      const id = params.get('id');
      this.currentId.set(id);
      if (id) void this.store.load(id);
    });
    void this.loadOptions();
    effect(() => {
      const caseId = this.store.task()?.caseId;
      if (this.canReadCases && caseId) void this.loadCaseName(caseId);
      else this.caseName.set('');
    });
  }
  protected async changeState(status: TaskState): Promise<void> {
    const id = this.currentId();
    if (id) await this.store.changeState(id, status);
  }
  protected async addChecklist(): Promise<void> {
    const title = this.checklistForm.controls.title.value.trim();
    const id = this.currentId();
    if (!id || this.checklistForm.invalid || this.store.busy()) return;
    if ((await this.store.addChecklist(id, title)) && this.alive && id === this.currentId())
      this.checklistForm.reset();
  }
  protected async setChecklist(itemId: string, completed: boolean): Promise<void> {
    const id = this.currentId();
    if (id) await this.store.setChecklist(id, itemId, completed);
  }
  protected async addComment(): Promise<void> {
    const text = this.commentForm.controls.text.value.trim();
    const id = this.currentId();
    if (!id || this.commentForm.invalid || !text || this.store.busy()) return;
    if ((await this.store.comment(id, text)) && this.alive && id === this.currentId())
      this.commentForm.reset();
  }
  protected async addSubtask(): Promise<void> {
    const parent = this.store.task();
    const id = this.currentId();
    if (!parent || !id || this.subtaskForm.invalid || this.store.busy()) return;
    const value = this.subtaskForm.getRawValue();
    const body: TaskInput = {
      ...value,
      caseId: parent.caseId,
      description: value.description || null,
    };
    if ((await this.store.subtask(id, body)) && this.alive && id === this.currentId())
      this.subtaskForm.reset({
        title: '',
        dueOn: '',
        assigneeUserId: '',
        priority: 'normal',
        description: '',
      });
  }
  protected memberName(id: string): string {
    return this.memberNames().get(id) ?? '';
  }
  protected hasOpenDependencies(): boolean {
    const task = this.store.task();
    return (
      !!task &&
      (task.checklist.some((x) => !x.completed) ||
        (!task.parentTaskId && !this.store.subtasksLoaded()) ||
        this.store.subtasks().some((x) => x.status !== 'completed' && x.status !== 'cancelled'))
    );
  }
  protected loadMoreSubtasks(): void {
    const id = this.currentId();
    if (id) void this.store.loadMoreSubtasks(id);
  }
  private async loadOptions(): Promise<void> {
    const result = await this.api.options();
    if (!this.alive || !result.ok) return;
    this.members.set(result.value.members);
    this.memberNames.set(new Map(this.members().map((x) => [x.userId, x.name])));
  }
  private async loadCaseName(id: string): Promise<void> {
    const request = ++this.caseVersion;
    const result = await this.api.caseDetail(id);
    if (!this.alive || request !== this.caseVersion) return;
    this.caseName.set(result.ok ? (result.value.cnjNumber ?? result.value.internalFolder) : '');
  }
}
