const TASK_ERROR_KEYS: Record<string, string> = {
  'tasks.filterLimit': 'tasks.filterLimit',
  'auth.forbidden': 'tasks.errors.forbidden',
  'scheduling.task_not_found': 'tasks.errors.notFound',
  'scheduling.task_invalid': 'tasks.errors.invalidTask',
  'scheduling.task_transition_invalid': 'tasks.errors.transitionInvalid',
  'scheduling.task_unresolved': 'tasks.errors.unresolved',
  'scheduling.checklist_invalid': 'tasks.errors.checklistInvalid',
  'scheduling.checklist_not_found': 'tasks.errors.checklistNotFound',
  'scheduling.comment_invalid': 'tasks.errors.commentInvalid',
  'scheduling.subtask_invalid': 'tasks.errors.subtaskInvalid',
  'scheduling.assignee_unavailable': 'tasks.errors.assigneeUnavailable',
  'scheduling.case_unavailable': 'tasks.errors.caseUnavailable',
  'scheduling.agenda_invalid': 'tasks.errors.listInvalid',
  'scheduling.batch_invalid': 'tasks.errors.batchInvalid',
  'platform.preference_not_found': 'tasks.errors.generic',
};

export function taskErrorTranslationKey(code: string | null | undefined): string {
  return (code && TASK_ERROR_KEYS[code]) || 'tasks.errors.generic';
}
