import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../../core/http/app-error';
import { CreateTaskRequest, Task, UpdateTaskRequest } from './task.model';
import { TasksApi } from './tasks-api';

/** Provided by `TasksPage`, so its state lives and dies with the page. */
@Injectable()
export class TasksStore {
  private readonly api = inject(TasksApi);

  private readonly tasksState = signal<Task[]>([]);
  private readonly loadingState = signal(false);
  private readonly errorState = signal<AppError | null>(null);

  readonly tasks = this.tasksState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();

  async load(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);
    try {
      this.tasksState.set(await firstValueFrom(this.api.list()));
    } catch (e) {
      this.errorState.set(e as AppError);
    } finally {
      this.loadingState.set(false);
    }
  }

  async create(req: CreateTaskRequest): Promise<Task> {
    const task = await firstValueFrom(this.api.create(req));
    this.tasksState.update((tasks) => [task, ...tasks]);
    return task;
  }

  /** Saves the task and swaps it in at the same position; on failure the list is left as it was. */
  async update(id: string, req: UpdateTaskRequest): Promise<Task> {
    const task = await firstValueFrom(this.api.update(id, req));
    this.tasksState.update((tasks) => tasks.map((t) => (t.id === id ? task : t)));
    return task;
  }

  /**
   * Deletes the task and drops it from the list. The API's task-not-found 404 means it is already gone,
   * so it is dropped too; any other 404 (wrong route, proxy) is rethrown and the task is kept.
   */
  async remove(id: string): Promise<void> {
    try {
      await firstValueFrom(this.api.delete(id), { defaultValue: undefined });
    } catch (e) {
      const err = e as AppError;
      if (err.status !== 404 || err.code !== 'tasks.not_found') {
        throw e;
      }
    }
    this.tasksState.update((tasks) => tasks.filter((t) => t.id !== id));
  }
}
