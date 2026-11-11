export type TaskStatus = 'ToDo' | 'InProgress' | 'Done';

export const TASK_STATUSES: readonly TaskStatus[] = ['ToDo', 'InProgress', 'Done'];

export const STATUS_LABELS: Record<TaskStatus, string> = {
  ToDo: 'To do',
  InProgress: 'In progress',
  Done: 'Done',
};

export interface Task {
  id: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  dueDate: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTaskRequest {
  title: string;
  description?: string | null;
  status?: TaskStatus;
  dueDate?: string | null;
}

/** Replaces every editable field of a task (PUT). */
export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  status: TaskStatus;
  dueDate: string | null;
}
