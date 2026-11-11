import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AppError } from '../../../core/http/app-error';
import { CreateTaskRequest, Task } from './task.model';
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
}
