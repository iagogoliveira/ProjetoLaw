import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import type { TaskInput, TaskPriority } from '../../../api/types.gen';
import { AccessStore } from '../../../core/organization/access-store';
import { TasksApi } from '../data/tasks-api';
import { taskErrorTranslationKey } from '../data/task-error-key';

const PRIORITIES: TaskPriority[] = ['low', 'normal', 'high', 'urgent'];
@Component({
  selector: 'lf-task-editor-page',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslocoDirective,
  ],
  templateUrl: './task-editor-page.html',
  styleUrls: ['../../../shared/ui/page.scss', './task-editor-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskEditorPage {
  protected readonly errorTranslationKey = taskErrorTranslationKey;
  protected readonly priorities = PRIORITIES;
  protected readonly members = signal<{ userId: string; name: string }[]>([]);
  protected readonly caseOptions = signal<{ id: string; label: string }[]>([]);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly taskId = signal<string | null>(null);
  protected readonly closedTask = signal(false);
  protected readonly resourceUnavailable = signal(false);
  protected readonly canReadCases = inject(AccessStore).can('cases.read');
  private readonly destroyRef = inject(DestroyRef);
  private alive = true;
  private requestVersion = 0;
  private caseSearchVersion = 0;
  private taskLoadVersion = 0;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', Validators.maxLength(4000)],
    dueOn: ['', Validators.required],
    assigneeUserId: ['', Validators.required],
    priority: ['normal' as TaskPriority, Validators.required],
    caseId: [''],
  });
  protected readonly caseSearch = inject(FormBuilder).nonNullable.control('');
  private readonly api = inject(TasksApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  constructor() {
    this.destroyRef.onDestroy(() => {
      this.alive = false;
      this.requestVersion++;
      this.caseSearchVersion++;
    });
    this.route.paramMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      const request = ++this.requestVersion;
      const taskId = params.get('id');
      this.taskId.set(taskId);
      this.closedTask.set(false);
      this.resourceUnavailable.set(false);
      this.errorCode.set(null);
      if (taskId) {
        this.clearForm();
        this.form.disable();
        void this.loadTask(taskId, request);
      } else {
        this.caseOptions.set([]);
        this.form.controls.title.enable();
        this.form.controls.description.enable();
        this.form.controls.dueOn.enable();
        this.form.controls.assigneeUserId.enable();
        this.form.controls.priority.enable();
        this.form.controls.caseId.enable();
        this.form.enable();
        this.form.reset({
          title: '',
          description: '',
          dueOn: '',
          assigneeUserId: '',
          priority: 'normal',
          caseId: '',
        });
      }
    });
    void this.loadOptions();
  }
  protected async searchCases(query: string): Promise<void> {
    const request = ++this.caseSearchVersion;
    if (!this.canReadCases || !query.trim()) {
      this.caseOptions.set([]);
      return;
    }
    const result = await this.api.cases(query.trim());
    if (!this.alive || request !== this.caseSearchVersion) return;
    this.caseOptions.set(
      result.ok
        ? result.value.items.map((x) => ({ id: x.id, label: x.cnjNumber ?? x.internalFolder }))
        : [],
    );
  }
  protected async save(): Promise<void> {
    if (this.busy() || this.closedTask()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const request = this.requestVersion;
    const taskId = this.taskId();
    this.busy.set(true);
    this.errorCode.set(null);
    const value = this.form.getRawValue();
    const body: TaskInput = {
      ...value,
      caseId: value.caseId || null,
      description: value.description || null,
    };
    const result = taskId ? await this.api.edit(taskId, body) : await this.api.create(body);
    if (!this.alive || request !== this.requestVersion) return;
    this.busy.set(false);
    if (!result.ok) {
      if (this.isUnavailableError(result.code)) this.clearUnavailableResource(result.code);
      else this.errorCode.set(result.code);
      return;
    }
    await this.router.navigate(['/tarefas', result.value.id]);
  }
  private async loadOptions(): Promise<void> {
    const result = await this.api.options();
    if (!this.alive) return;
    if (result.ok) this.members.set(result.value.members);
    else this.errorCode.set(result.code);
  }
  private async loadTask(id: string, request: number): Promise<void> {
    const taskLoad = ++this.taskLoadVersion;
    this.caseOptions.set([]);
    this.busy.set(true);
    const result = await this.api.detail(id);
    if (!this.alive || request !== this.requestVersion || taskLoad !== this.taskLoadVersion) return;
    if (!result.ok) {
      this.busy.set(false);
      if (this.isUnavailableError(result.code)) this.clearUnavailableResource(result.code);
      else this.errorCode.set(result.code);
      return;
    }
    this.form.patchValue({
      title: result.value.title,
      description: result.value.description ?? '',
      dueOn: result.value.dueOn,
      assigneeUserId: result.value.assigneeUserId,
      priority: result.value.priority,
      caseId: result.value.caseId ?? '',
    });
    const closed = result.value.status === 'completed' || result.value.status === 'cancelled';
    this.closedTask.set(closed);
    if (closed) {
      this.form.controls.title.disable();
      this.form.controls.description.disable();
      this.form.controls.dueOn.disable();
      this.form.controls.assigneeUserId.disable();
      this.form.controls.priority.disable();
    } else {
      this.form.controls.title.enable();
      this.form.controls.description.enable();
      this.form.controls.dueOn.enable();
      this.form.controls.assigneeUserId.enable();
      this.form.controls.priority.enable();
    }
    this.form.controls.caseId.disable();
    this.busy.set(false);
    if (this.canReadCases && result.value.caseId) {
      const caseResponse = await this.api.caseDetail(result.value.caseId);
      if (!this.alive || request !== this.requestVersion || taskLoad !== this.taskLoadVersion)
        return;
      if (caseResponse.ok)
        this.caseOptions.set([
          {
            id: caseResponse.value.id,
            label: caseResponse.value.cnjNumber ?? caseResponse.value.internalFolder,
          },
        ]);
    }
  }
  private isUnavailableError(code: string): boolean {
    return ['auth.forbidden', 'scheduling.task_not_found', 'scheduling.case_unavailable'].includes(
      code,
    );
  }
  private clearForm(): void {
    this.form.reset({
      title: '',
      description: '',
      dueOn: '',
      assigneeUserId: '',
      priority: 'normal',
      caseId: '',
    });
    this.caseSearch.reset('');
    this.caseOptions.set([]);
  }
  private clearUnavailableResource(code: string): void {
    this.resourceUnavailable.set(true);
    this.errorCode.set(code);
    this.closedTask.set(false);
    this.clearForm();
    this.form.disable();
  }
}
