import { Injectable, computed, inject, signal } from '@angular/core';
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

  private readonly nextCursor = signal<string | null>(null);
  private readonly loadingMoreState = signal(false);
  private readonly loadMoreErrorState = signal<AppError | null>(null);

  readonly tasks = this.tasksState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly loadingMore = this.loadingMoreState.asReadonly();
  readonly loadMoreError = this.loadMoreErrorState.asReadonly();
  readonly hasMore = computed(() => this.nextCursor() !== null);

  async load(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);
    this.loadMoreErrorState.set(null);
    this.nextCursor.set(null);
    try {
      const page = await firstValueFrom(this.api.list());
      this.tasksState.set(page.items);
      this.nextCursor.set(page.nextCursor);
    } catch (e) {
      this.errorState.set(e as AppError);
    } finally {
      this.loadingState.set(false);
    }
  }

  async loadMore(): Promise<void> {
    const cursor = this.nextCursor();
    if (this.loadingMoreState() || cursor === null) return;
    this.loadingMoreState.set(true);
    this.loadMoreErrorState.set(null);
    try {
      const page = await firstValueFrom(this.api.list({ cursor }));
      this.tasksState.update((tasks) => {
        const seen = new Set(tasks.map((t) => t.id));
        return [...tasks, ...page.items.filter((t) => !seen.has(t.id))];
      });
      this.nextCursor.set(page.nextCursor);
    } catch (e) {
      this.loadMoreErrorState.set(e as AppError);
    } finally {
      this.loadingMoreState.set(false);
    }
  }

  async create(req: CreateTaskRequest): Promise<Task> {
    const task = await firstValueFrom(this.api.create(req));
    this.tasksState.update((tasks) => [task, ...tasks]);
    return task;
  }
}
