import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { MatSelect } from '@angular/material/select';
import { describe, expect, it, vi } from 'vitest';
import type { TaskDetailResponse, TaskListResponse } from '../../../api/types.gen';
import { TasksApi, type TaskFilters } from './tasks-api';
import { readSavedTaskFilters, TasksStore } from './tasks-store';
import { TaskDetailStore } from './task-detail-store';
import { AccessStore } from '../../../core/organization/access-store';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import { TaskDetailPage } from '../pages/task-detail-page';
import { TaskEditorPage } from '../pages/task-editor-page';
import { TasksListPage } from '../pages/tasks-list-page';
import { taskErrorTranslationKey } from './task-error-key';

const filters: TaskFilters = {
  status: null,
  priority: null,
  assigneeUserId: null,
  teamId: null,
  caseId: null,
};
const task: TaskDetailResponse = {
  id: 'task-1',
  caseId: null,
  publicationId: null,
  parentTaskId: null,
  title: 'Review',
  description: null,
  dueOn: '2026-10-30',
  assigneeUserId: 'user-1',
  status: 'pending',
  priority: 'normal',
  closedBy: null,
  closedAt: null,
  checklist: [],
  comments: [],
};
const page = (items: TaskDetailResponse[], nextCursor: string | null = null): TaskListResponse => ({
  items,
  nextCursor,
});

describe('TasksStore', () => {
  it('exposes standalone task list, editor, and detail pages for the feature routes', () => {
    expect(TasksListPage).toBeDefined();
    expect(TaskEditorPage).toBeDefined();
    expect(TaskDetailPage).toBeDefined();
  });

  function setup(api: Record<string, ReturnType<typeof vi.fn>>) {
    TestBed.configureTestingModule({
      providers: [TasksStore, { provide: TasksApi, useValue: api }],
    });
    return TestBed.inject(TasksStore);
  }

  it('preserves labelled partial redistribution results after refreshing the list', async () => {
    const closed = { ...task, id: 'closed', title: 'Closed task', status: 'cancelled' as const };
    const list = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, value: page([task, closed]) })
      .mockResolvedValueOnce({ ok: true, value: page([task]) });
    const redistribute = vi.fn().mockResolvedValue({
      ok: true,
      value: {
        items: [
          { id: task.id, changed: true, errorCode: null },
          { id: closed.id, changed: false, errorCode: 'scheduling.task_transition_invalid' },
        ],
      },
    });
    const store = setup({ list, redistribute });
    await store.search(filters);
    await store.redistribute([task.id, closed.id], 'new-assignee');
    expect(store.redistribution()).toEqual([
      { id: task.id, title: task.title, changed: true, errorCode: null },
      {
        id: closed.id,
        title: closed.title,
        changed: false,
        errorCode: 'scheduling.task_transition_invalid',
      },
    ]);
    expect(store.items()).toEqual([task]);
    expect(store.busy()).toBe(false);
  });

  it('uses the cursor from the last server page and rejects stale searches', async () => {
    const resolvers: ((result: { ok: true; value: TaskListResponse }) => void)[] = [];
    const list = vi.fn(() =>
      resolvers.length >= 2
        ? Promise.resolve({ ok: true as const, value: page([{ ...task, id: 'more' }]) })
        : new Promise<{ ok: true; value: TaskListResponse }>((resolve) => resolvers.push(resolve)),
    );
    const store = setup({ list });
    const first = store.search({ ...filters, caseId: 'case-a' });
    const second = store.search({ ...filters, caseId: 'case-b' });
    resolvers[1]({ ok: true, value: page([{ ...task, id: 'current' }], 'server-cursor') });
    await second;
    resolvers[0]({ ok: true, value: page([{ ...task, id: 'stale' }]) });
    await first;
    await store.loadMore();
    expect(store.items().map((x) => x.id)).toEqual(['current', 'more']);
    expect(list).toHaveBeenLastCalledWith({ ...filters, caseId: 'case-b' }, 'server-cursor');
    expect(store.hasMore()).toBe(false);
  });

  it('clears the cursor on a failed first page and does not invent a total', async () => {
    const list = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, value: page([task], 'cursor') })
      .mockResolvedValueOnce({ ok: false, code: 'scheduling.task_list_failed' });
    const store = setup({ list });
    await store.search(filters);
    expect(store.hasMore()).toBe(true);
    await store.search({ ...filters, priority: 'urgent' });
    expect(store.items()).toEqual([]);
    expect(store.nextCursor()).toBeNull();
    expect(store.hasMore()).toBe(false);
  });

  it('reloads the active filter after a successful state change', async () => {
    const list = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, value: page([task]) })
      .mockResolvedValueOnce({ ok: true, value: page([]) });
    const store = setup({
      list,
      state: vi.fn().mockResolvedValue({ ok: true, value: { ...task, status: 'completed' } }),
    });
    await store.search({ ...filters, status: 'pending' });
    expect(store.items()).toHaveLength(1);
    await store.changeState(task.id, 'completed');
    expect(list).toHaveBeenCalledTimes(2);
    expect(list).toHaveBeenLastCalledWith({ ...filters, status: 'pending' }, null);
    expect(store.items()).toEqual([]);
    expect(store.busy()).toBe(false);
  });

  it.each([
    ['load denied', 'auth.forbidden', true],
    ['save denied', 'auth.forbidden', false],
    ['load not found', 'scheduling.task_not_found', true],
  ])('clears sensitive editor data after %s', async (_scenario, code, isLoad) => {
    const routeParams = new BehaviorSubject(convertToParamMap(isLoad ? { id: task.id } : {}));
    const api = {
      options: vi.fn().mockResolvedValue({ ok: true, value: { members: [], teams: [] } }),
      detail: vi.fn().mockResolvedValue({ ok: false, code }),
      create: vi.fn().mockResolvedValue({ ok: false, code }),
      edit: vi.fn().mockResolvedValue({ ok: false, code }),
      cases: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [TaskEditorPage, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: routeParams } },
        { provide: TasksApi, useValue: api },
        { provide: AccessStore, useValue: { can: () => false } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(TaskEditorPage);
    await fixture.whenStable();
    fixture.detectChanges();
    const editor = fixture.componentInstance as unknown as {
      form: {
        patchValue(value: object): void;
        getRawValue(): {
          title: string;
          description: string;
          dueOn: string;
          assigneeUserId: string;
          priority: string;
          caseId: string;
        };
      };
      save(): Promise<void>;
      resourceUnavailable(): boolean;
    };
    if (!isLoad) {
      editor.form.patchValue({
        title: 'Confidential draft',
        dueOn: '2026-10-30',
        assigneeUserId: 'user-1',
      });
      await editor.save();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    }
    expect(editor.form.getRawValue()).toEqual({
      title: '',
      description: '',
      dueOn: '',
      assigneeUserId: '',
      priority: 'normal',
      caseId: '',
    });
    expect(editor.resourceUnavailable()).toBe(true);
    expect(fixture.nativeElement.querySelector('button[type="submit"]')).toBeNull();
    fixture.destroy();
  });

  it('clears the page and cursor when loading a later page fails', async () => {
    const list = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, value: page([task], 'cursor') })
      .mockResolvedValueOnce({ ok: false, code: 'auth.forbidden' });
    const store = setup({ list });
    await store.search(filters);
    await store.loadMore();
    expect(store.items()).toEqual([]);
    expect(store.nextCursor()).toBeNull();
    expect(store.errorCode()).toBe('auth.forbidden');
  });

  it('releases a stale mutation busy state when the search context changes', async () => {
    let finish!: (result: { ok: true; value: TaskDetailResponse }) => void;
    const list = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, value: page([task]) })
      .mockResolvedValueOnce({ ok: true, value: page([{ ...task, title: 'Filtered task' }]) });
    const state = vi.fn(
      () =>
        new Promise<{ ok: true; value: TaskDetailResponse }>((resolve) => {
          finish = resolve;
        }),
    );
    const store = setup({ list, state });
    await store.search(filters);
    const mutation = store.changeState(task.id, 'completed');
    expect(store.busy()).toBe(true);
    await store.search({ ...filters, priority: 'urgent' });
    expect(store.busy()).toBe(false);
    finish({ ok: true, value: { ...task, status: 'completed' } });
    await mutation;
    expect(store.busy()).toBe(false);
    expect(store.items()[0]?.title).toBe('Filtered task');
  });

  it('releases stale detail mutations and preserves the newly loaded task', async () => {
    let finish!: (result: { ok: true; value: TaskDetailResponse }) => void;
    const detail = vi.fn(async (id: string) => ({
      ok: true as const,
      value: { ...task, id, title: id },
    }));
    const store = setupDetail({
      detail,
      subtasks: vi.fn().mockResolvedValue({ ok: true, value: page([]) }),
      state: vi.fn(
        () =>
          new Promise<{ ok: true; value: TaskDetailResponse }>((resolve) => {
            finish = resolve;
          }),
      ),
    });
    await store.load('task-before');
    const mutation = store.changeState('task-before', 'completed');
    expect(store.busy()).toBe(true);
    await store.load('task-after');
    expect(store.busy()).toBe(false);
    finish({ ok: true, value: { ...task, id: 'task-before', status: 'completed' } });
    await mutation;
    expect(store.busy()).toBe(false);
    expect(store.task()?.title).toBe('task-after');
  });

  it('clears the parent and subtasks when subtask creation loses access', async () => {
    const store = setupDetail({
      detail: vi.fn().mockResolvedValue({ ok: true, value: task }),
      subtasks: vi.fn().mockResolvedValue({ ok: true, value: page([{ ...task, id: 'child' }]) }),
      subtask: vi.fn().mockResolvedValue({ ok: false, code: 'scheduling.task_not_found' }),
    });
    await store.load(task.id);
    expect(store.subtasks()).toHaveLength(1);
    const created = await store.subtask(task.id, {
      caseId: null,
      title: 'Child',
      description: null,
      dueOn: '2026-11-01',
      assigneeUserId: 'user-1',
      priority: 'normal',
    });
    expect(created).toBe(false);
    expect(store.task()).toBeNull();
    expect(store.subtasks()).toEqual([]);
  });

  it('updates task editor and detail DOM after awaited API calls', async () => {
    const routeParams = new BehaviorSubject(convertToParamMap({}));
    let finishCreate!: (result: { ok: true; value: TaskDetailResponse }) => void;
    const caseOption = {
      id: '123e4567-e89b-12d3-a456-426614174001',
      label: 'CNJ 0000001',
    };
    const api = {
      options: vi.fn().mockResolvedValue({
        ok: true,
        value: { members: [{ userId: 'user-1', name: 'Ana Silva' }], teams: [] },
      }),
      cases: vi.fn().mockResolvedValue({
        ok: true,
        value: {
          items: [{ ...caseOption, cnjNumber: caseOption.label, internalFolder: 'Pasta 1' }],
        },
      }),
      create: vi.fn(
        () =>
          new Promise<{ ok: true; value: TaskDetailResponse }>((resolve) => {
            finishCreate = resolve;
          }),
      ),
      detail: vi.fn().mockResolvedValue({ ok: true, value: task }),
      subtasks: vi.fn().mockResolvedValue({ ok: true, value: page([]) }),
      state: vi.fn().mockResolvedValue({ ok: true, value: { ...task, status: 'completed' } }),
      caseDetail: vi.fn().mockResolvedValue({
        ok: true,
        value: { id: caseOption.id, cnjNumber: caseOption.label, internalFolder: 'Pasta 1' },
      }),
      list: vi.fn().mockResolvedValue({ ok: true, value: page([]) }),
      preference: vi.fn().mockResolvedValue({ ok: false, code: 'platform.preference_not_found' }),
    };
    await TestBed.configureTestingModule({
      imports: [TasksListPage, TaskEditorPage, TaskDetailPage, translocoTesting()],
      providers: [
        provideRouter([{ path: 'tarefas/:id', component: TaskDetailPage }]),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: routeParams, queryParamMap: routeParams },
        },
        { provide: TasksApi, useValue: api },
        {
          provide: AccessStore,
          useValue: {
            can: () => true,
            permissions: () => new Set(['schedule.tasks.manage', 'cases.read']),
          },
        },
      ],
    }).compileComponents();

    const editorFixture = TestBed.createComponent(TaskEditorPage);
    await editorFixture.whenStable();
    editorFixture.detectChanges();
    const selects = editorFixture.debugElement.queryAll(By.directive(MatSelect));
    selects[0]?.injector.get(MatSelect).open();
    editorFixture.detectChanges();
    await editorFixture.whenStable();
    expect(document.body.textContent).toContain('Ana Silva');
    selects[0]?.injector.get(MatSelect).close();
    const editor = editorFixture.componentInstance as unknown as {
      searchCases(query: string): Promise<void>;
      form: {
        patchValue(value: object): void;
        get(value: string): { value: string } | null;
        getRawValue(): { caseId: string };
      };
    };
    editor.form.patchValue({ title: 'Draft title' });
    await editor.searchCases('CNJ');
    editorFixture.detectChanges();
    selects[2]?.injector.get(MatSelect).open();
    editorFixture.detectChanges();
    await editorFixture.whenStable();
    expect(document.body.textContent).toContain('CNJ 0000001');
    expect(editor.form.get('title')?.value).toBe('Draft title');
    const caseOptions = document.body.querySelectorAll('mat-option') as NodeListOf<HTMLElement>;
    const caseOptionElement = Array.from(caseOptions).find((option) =>
      option.textContent?.includes('CNJ 0000001'),
    );
    expect(caseOptionElement).toBeDefined();
    caseOptionElement?.click();
    editorFixture.detectChanges();
    expect(editor.form.getRawValue().caseId).toBe(caseOption.id);
    editor.form.patchValue({
      title: 'Prepare filing',
      description: '',
      dueOn: '2026-10-30',
      assigneeUserId: 'user-1',
      priority: 'normal',
      caseId: editor.form.getRawValue().caseId,
    });
    const saveButton = editorFixture.nativeElement.querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;
    saveButton.click();
    editorFixture.detectChanges();
    expect(saveButton.disabled).toBe(true);
    finishCreate({ ok: true, value: { ...task, id: 'created-task', caseId: caseOption.id } });
    await editorFixture.whenStable();
    editorFixture.detectChanges();
    expect(api.create).toHaveBeenCalledWith(expect.objectContaining({ caseId: caseOption.id }));
    expect(editorFixture.nativeElement.textContent).not.toContain('errors.');
    editorFixture.destroy();

    routeParams.next(convertToParamMap({ id: task.id }));
    const detailFixture = TestBed.createComponent(TaskDetailPage);
    await detailFixture.whenStable();
    detailFixture.detectChanges();
    expect(detailFixture.nativeElement.textContent).toContain('Pendente');
    const detail = detailFixture.componentInstance as unknown as {
      changeState(status: 'completed'): Promise<void>;
    };
    await detail.changeState('completed');
    detailFixture.detectChanges();
    expect(detailFixture.nativeElement.textContent).toContain('Concluída');
    detailFixture.destroy();

    routeParams.next(convertToParamMap({}));
    const listFixture = TestBed.createComponent(TasksListPage);
    await listFixture.whenStable();
    const listPage = listFixture.componentInstance as unknown as {
      selectedIds: Set<string>;
    };
    listPage.selectedIds.add(task.id);
    routeParams.next(convertToParamMap({ status: 'pending' }));
    await listFixture.whenStable();
    expect(listPage.selectedIds.size).toBe(0);
  });

  it('validates saved preference data and keeps at most ten unique filters', () => {
    const id = '123e4567-e89b-12d3-a456-426614174000';
    const entries = Array.from({ length: 12 }, (_, index) => ({
      name: 'Filter ' + index,
      filters: { ...filters, caseId: id },
    }));
    const saved = readSavedTaskFilters({
      saved: [{ name: 'invalid', filters: { ...filters, status: 'unknown' } }, ...entries],
    });
    expect(saved).toHaveLength(10);
    expect(saved[0]?.name).toBe('Filter 0');
    expect(
      readSavedTaskFilters({
        saved: [
          { name: 'same', filters },
          { name: 'same', filters },
        ],
      }),
    ).toHaveLength(1);
    expect(readSavedTaskFilters(null)).toEqual([]);
    expect(taskErrorTranslationKey('scheduling.task_unresolved')).toBe('tasks.errors.unresolved');
    expect(taskErrorTranslationKey('scheduling.unknown')).toBe('tasks.errors.generic');
  });

  function setupDetail(api: Record<string, ReturnType<typeof vi.fn>>) {
    TestBed.configureTestingModule({
      providers: [TaskDetailStore, { provide: TasksApi, useValue: api }],
    });
    return TestBed.inject(TaskDetailStore);
  }
});
