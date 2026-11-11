import { ChangeDetectionStrategy, Component, OnInit, inject, signal, viewChild } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppError } from '../../../core/http/app-error';
import { STATUS_LABELS, Task, TaskStatus, UpdateTaskRequest } from '../data/task.model';
import { TasksStore } from '../data/tasks-store';
import { TaskForm } from '../task-form/task-form';
import { TaskList } from '../task-list/task-list';

@Component({
  selector: 'app-tasks-page',
  imports: [TaskForm, TaskList],
  providers: [TasksStore],
  templateUrl: './tasks-page.html',
  styleUrl: './tasks-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TasksPage implements OnInit {
  protected readonly store = inject(TasksStore);
  private readonly snackBar = inject(MatSnackBar);
  private readonly form = viewChild(TaskForm);

  /** The task loaded into the form; null means the form adds a new task. */
  protected readonly editing = signal<Task | null>(null);
  protected readonly submitting = signal(false);
  /** Ids of tasks whose move is still in flight. */
  protected readonly pendingMoves = signal<ReadonlySet<string>>(new Set());
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  protected readonly formError = signal<string | null>(null);

  ngOnInit(): void {
    void this.store.load();
  }

  protected onEdit(task: Task): void {
    this.clearErrors();
    this.editing.set(task);
  }

  protected onCancelEdit(): void {
    this.clearErrors();
    this.editing.set(null);
  }

  protected async onSubmitted(req: UpdateTaskRequest): Promise<void> {
    const editing = this.editing();
    this.submitting.set(true);
    this.clearErrors();
    try {
      if (editing) {
        await this.store.update(editing.id, req);
        this.editing.set(null);
        this.snackBar.open('Task updated', undefined, { duration: 3000 });
      } else {
        await this.store.create(req);
        this.form()?.reset();
        this.snackBar.open('Task created', undefined, { duration: 3000 });
      }
    } catch (e) {
      const err = e as AppError;
      const fieldErrors = err.fieldErrors ?? {};
      if (Object.keys(fieldErrors).length > 0) {
        this.serverErrors.set(fieldErrors);
      } else {
        this.formError.set(err.message);
      }
    } finally {
      this.submitting.set(false);
    }
  }

  protected async onMove({ task, status }: { task: Task; status: TaskStatus }): Promise<void> {
    this.pendingMoves.update((ids) => new Set(ids).add(task.id));
    try {
      await this.store.update(task.id, {
        title: task.title,
        description: task.description,
        status,
        dueDate: task.dueDate,
      });
      this.snackBar.open(`Moved to ${STATUS_LABELS[status]}`, undefined, { duration: 3000 });
    } catch (e) {
      const err = e as AppError;
      const reason = Object.values(err.fieldErrors ?? {})[0]?.[0] ?? err.message;
      this.snackBar.open(`Couldn't move the task: ${reason}`, undefined, { duration: 5000 });
    } finally {
      this.pendingMoves.update((ids) => {
        const next = new Set(ids);
        next.delete(task.id);
        return next;
      });
    }
  }

  private clearErrors(): void {
    this.serverErrors.set({});
    this.formError.set(null);
  }
}
