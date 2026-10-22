import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import type { TaskDetailResponse, TaskInput, TaskState } from '../../../api/types.gen';
import { TasksApi } from './tasks-api';

interface DetailState {
  task: TaskDetailResponse | null;
  subtasks: TaskDetailResponse[];
  subtaskCursor: string | null;
  subtasksLoaded: boolean;
  loading: boolean;
  busy: boolean;
  errorCode: string | null;
}
export const TaskDetailStore = signalStore(
  withState<DetailState>({
    task: null,
    subtasks: [],
    subtaskCursor: null,
    subtasksLoaded: false,
    loading: false,
    busy: false,
    errorCode: null,
  }),
  withMethods((store, api = inject(TasksApi)) => {
    let version = 0;
    async function load(id: string): Promise<void> {
      const request = ++version;
      patchState(store, {
        loading: true,
        busy: false,
        task: null,
        subtasks: [],
        subtaskCursor: null,
        subtasksLoaded: false,
        errorCode: null,
      });
      const result = await api.detail(id);
      if (request !== version) return;
      if (!result.ok) {
        patchState(store, {
          task: null,
          subtasks: [],
          subtaskCursor: null,
          subtasksLoaded: false,
          loading: false,
          errorCode: result.code,
        });
        return;
      }
      const subtaskResult = result.value.parentTaskId ? null : await api.subtasks(id);
      if (request !== version) return;
      patchState(store, {
        task: result.value,
        subtasks: subtaskResult?.ok ? subtaskResult.value.items : [],
        subtaskCursor: subtaskResult?.ok ? subtaskResult.value.nextCursor : null,
        subtasksLoaded: Boolean(subtaskResult?.ok),
        errorCode: subtaskResult && !subtaskResult.ok ? subtaskResult.code : null,
        loading: false,
      });
    }
    async function update(
      id: string,
      operation: (
        id: string,
      ) => Promise<{ ok: boolean; value?: TaskDetailResponse; code?: string }>,
    ): Promise<boolean> {
      if (store.busy()) return false;
      const request = version;
      patchState(store, { busy: true, errorCode: null });
      const result = await operation(id);
      if (request !== version) return false;
      if (!result.ok) {
        const errorCode = result.code ?? 'http.error';
        patchState(store, { busy: false, errorCode });
        await load(id);
        if (request + 1 === version) patchState(store, { errorCode });
        return false;
      }
      patchState(store, { task: result.value ?? store.task(), busy: false });
      if (result.value && !result.value.parentTaskId) {
        const subtasks = await api.subtasks(id);
        if (request === version)
          patchState(
            store,
            subtasks.ok
              ? {
                  subtasks: subtasks.value.items,
                  subtaskCursor: subtasks.value.nextCursor,
                  subtasksLoaded: true,
                }
              : {
                  subtasks: [],
                  subtaskCursor: null,
                  subtasksLoaded: false,
                  errorCode: subtasks.code,
                },
          );
      }
      return true;
    }
    async function loadMoreSubtasks(id: string): Promise<void> {
      const after = store.subtaskCursor();
      const request = version;
      if (!after || store.loading() || store.busy()) return;
      patchState(store, { loading: true });
      const result = await api.subtasks(id, after);
      if (request !== version) return;
      patchState(
        store,
        result.ok
          ? {
              subtasks: [...store.subtasks(), ...result.value.items],
              subtaskCursor: result.value.nextCursor,
              loading: false,
            }
          : {
              subtasks: [],
              subtaskCursor: null,
              subtasksLoaded: false,
              loading: false,
              errorCode: result.code,
            },
      );
    }
    async function addSubtask(id: string, body: TaskInput): Promise<boolean> {
      if (store.busy()) return false;
      const request = version;
      patchState(store, { busy: true, errorCode: null });
      const result = await api.subtask(id, body);
      if (request !== version) return false;
      if (!result.ok) {
        const clearResource = [
          'auth.forbidden',
          'scheduling.task_not_found',
          'scheduling.case_unavailable',
        ].includes(result.code ?? '');
        patchState(
          store,
          clearResource
            ? {
                busy: false,
                errorCode: result.code,
                task: null,
                subtasks: [],
                subtaskCursor: null,
                subtasksLoaded: false,
              }
            : { busy: false, errorCode: result.code },
        );
        return false;
      }
      patchState(store, { busy: false });
      await load(id);
      return true;
    }
    return {
      load,
      changeState: (id: string, status: TaskState) =>
        update(id, (taskId) => api.state(taskId, status)),
      addChecklist: (id: string, title: string) =>
        update(id, (taskId) => api.addChecklist(taskId, title)),
      setChecklist: (id: string, itemId: string, completed: boolean) =>
        update(id, (taskId) => api.setChecklist(taskId, itemId, completed)),
      comment: (id: string, text: string) => update(id, (taskId) => api.comment(taskId, text)),
      subtask: addSubtask,
      loadMoreSubtasks,
    };
  }),
);
