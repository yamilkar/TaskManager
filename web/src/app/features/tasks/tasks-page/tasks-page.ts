import { CdkTrapFocus } from '@angular/cdk/a11y';
import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppError } from '../../../core/http/app-error';
import { STATUS_LABELS, TASK_STATUSES, Task, TaskStatus, UpdateTaskRequest } from '../data/task.model';
import { TasksStore } from '../data/tasks-store';
import { TaskForm } from '../task-form/task-form';
import { TaskList } from '../task-list/task-list';

@Component({
  selector: 'app-tasks-page',
  imports: [CdkTrapFocus, TaskForm, TaskList],
  providers: [TasksStore],
  templateUrl: './tasks-page.html',
  styleUrl: './tasks-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TasksPage implements OnInit {
  protected readonly store = inject(TasksStore);
  private readonly snackBar = inject(MatSnackBar);
  private readonly form = viewChild(TaskForm);
  private readonly injector = inject(Injector);

  protected readonly statuses = TASK_STATUSES;
  protected readonly labels = STATUS_LABELS;
  protected readonly counts = computed(() => {
    const counts: Record<TaskStatus, number> = { ToDo: 0, InProgress: 0, Done: 0 };
    for (const task of this.store.tasks()) {
      counts[task.status]++;
    }
    return counts;
  });

  /** The add or edit form is shown in a dialog; it is created on open, so it always starts fresh. */
  protected readonly dialogOpen = signal(false);
  /** Where focus goes back to when the dialog closes. */
  private returnFocusTo: HTMLElement | null = null;

  /** The task loaded into the form; null means the form adds a new task. */
  protected readonly editing = signal<Task | null>(null);
  protected readonly submitting = signal(false);
  /** Ids of tasks whose move or delete is still in flight. */
  protected readonly pending = signal<ReadonlySet<string>>(new Set());
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  protected readonly formError = signal<string | null>(null);

  ngOnInit(): void {
    void this.store.load();
  }

  protected openAdd(): void {
    this.open(null);
    afterNextRender(() => this.form()?.focusTitle(), { injector: this.injector });
  }

  /** The form focuses its title itself when it receives a task. */
  protected onEdit(task: Task): void {
    this.open(task);
  }

  protected closeDialog(): void {
    if (this.submitting()) {
      return;
    }
    this.clearErrors();
    this.editing.set(null);
    this.dialogOpen.set(false);
    const target = this.returnFocusTo;
    this.returnFocusTo = null;
    afterNextRender(() => target?.focus(), { injector: this.injector });
  }

  private open(task: Task | null): void {
    this.clearErrors();
    this.returnFocusTo = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.editing.set(task);
    this.dialogOpen.set(true);
  }

  protected async onSubmitted(req: UpdateTaskRequest): Promise<void> {
    const editing = this.editing();
    this.submitting.set(true);
    this.clearErrors();
    try {
      if (editing) {
        await this.store.update(editing.id, req);
        this.snackBar.open('Task updated', undefined, { duration: 3000 });
      } else {
        await this.store.create(req);
        this.snackBar.open('Task created', undefined, { duration: 3000 });
      }
      this.submitting.set(false);
      this.closeDialog();
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
    this.pending.update((ids) => new Set(ids).add(task.id));
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
      this.pending.update((ids) => {
        const next = new Set(ids);
        next.delete(task.id);
        return next;
      });
    }
  }

  protected async onDelete(task: Task): Promise<void> {
    this.pending.update((ids) => new Set(ids).add(task.id));
    try {
      await this.store.remove(task.id);
      this.snackBar.open('Task deleted', undefined, { duration: 3000 });
    } catch (e) {
      this.snackBar.open(`Couldn't delete the task: ${(e as AppError).message}`, undefined, { duration: 5000 });
    } finally {
      this.pending.update((ids) => {
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
