import { ChangeDetectionStrategy, Component, OnInit, inject, signal, viewChild } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppError } from '../../../core/http/app-error';
import { CreateTaskRequest } from '../data/task.model';
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

  protected readonly submitting = signal(false);
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  protected readonly formError = signal<string | null>(null);

  ngOnInit(): void {
    void this.store.load();
  }

  protected async onSubmitted(req: CreateTaskRequest): Promise<void> {
    this.submitting.set(true);
    this.serverErrors.set({});
    this.formError.set(null);
    try {
      await this.store.create(req);
      this.form()?.reset();
      this.snackBar.open('Task created', undefined, { duration: 3000 });
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
}
