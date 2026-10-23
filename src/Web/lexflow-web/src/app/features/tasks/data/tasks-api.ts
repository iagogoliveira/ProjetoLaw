import { Injectable } from '@angular/core';
import * as sdk from '../../../api/sdk.gen';
import type * as dto from '../../../api/types.gen';
import { toResult } from '../../../core/data/api-result';

export interface TaskFilters {
  status: dto.TaskState | null;
  priority: dto.TaskPriority | null;
  assigneeUserId: string | null;
  teamId: string | null;
  caseId: string | null;
}

@Injectable({ providedIn: 'root' })
export class TasksApi {
  list(filters: TaskFilters, after: string | null) {
    return toResult(
      sdk.listTasks({
        query: {
          status: filters.status ?? undefined,
          priority: filters.priority ?? undefined,
          assigneeUserId: filters.assigneeUserId ?? undefined,
          teamId: filters.teamId ?? undefined,
          caseId: filters.caseId ?? undefined,
          after: after ?? undefined,
          limit: 50,
        },
      }),
    );
  }
  subtasks(parentTaskId: string, after: string | null = null) {
    return toResult(
      sdk.listTasks({ query: { parentTaskId, after: after ?? undefined, limit: 50 } }),
    );
  }
  detail(id: string) {
    return toResult(sdk.getTask({ path: { id } }));
  }
  create(body: dto.TaskInput) {
    return toResult(sdk.registerTask({ body }));
  }
  edit(id: string, body: dto.TaskInput) {
    return toResult(sdk.editTask({ path: { id }, body }));
  }
  subtask(id: string, body: dto.TaskInput) {
    return toResult(sdk.registerSubtask({ path: { id }, body }));
  }
  state(id: string, status: dto.TaskState) {
    return toResult(sdk.changeTaskState({ path: { id }, body: { status } }));
  }
  addChecklist(id: string, title: string) {
    return toResult(sdk.addTaskChecklistItem({ path: { id }, body: { title } }));
  }
  setChecklist(id: string, itemId: string, completed: boolean) {
    return toResult(sdk.setTaskChecklistItem({ path: { id, itemId }, body: { completed } }));
  }
  comment(id: string, text: string) {
    return toResult(sdk.addTaskComment({ path: { id }, body: { text } }));
  }
  redistribute(ids: string[], assigneeUserId: string) {
    return toResult(sdk.redistributeTasks({ body: { ids, assigneeUserId } }));
  }
  options() {
    return toResult(sdk.getTaskOptions());
  }
  cases(q: string) {
    return toResult(sdk.listCases({ query: { Q: q, Status: 'active' } }));
  }
  caseDetail(id: string) {
    return toResult(sdk.getCase({ path: { id } }));
  }
  preference() {
    return toResult(sdk.getMyPreference({ query: { key: 'tasks.list' } }));
  }
  savePreference(value: dto.JsonElement) {
    return toResult(sdk.setMyPreference({ body: { key: 'tasks.list', value } }));
  }
}
