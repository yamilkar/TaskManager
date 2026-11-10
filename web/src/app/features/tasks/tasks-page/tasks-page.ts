import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppError } from '../../../core/http/app-error';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { CreateTaskRequest } from '../data/task.model';
import { TasksStore } from '../data/tasks-store';
import { TaskForm } from '../task-form/task-form';
import { TaskList } from '../task-list/task-list';

@Component({
  selector: 'app-tasks-page',
  imports: [MatButtonModule, EmptyState, TaskForm, TaskList],
  providers: [TasksStore],
  templateUrl: './tasks-page.html',
  styleUrl: './tasks-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TasksPage implements OnInit {
  protected readonly store = inject(TasksStore);
  private readonly snackBar = inject(MatSnackBar);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly form = viewChild(TaskForm);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  protected readonly submitting = signal(false);
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  protected readonly formError = signal<string | null>(null);

  ngOnInit(): void {
    void this.store.load();
  }

  protected async onLoadMore(): Promise<void> {
    const before = this.store.tasks().length;
    await this.store.loadMore();
    if (this.store.loadMoreError()) return; // the button is still there and keeps focus
    const added = this.store.tasks().length > before;
    // The button may be gone (last page) or was inert while loading: hand focus to the new content.
    afterNextRender(
      () => {
        const row = this.host.nativeElement.querySelectorAll<HTMLElement>('li.task')[before];
        (added && row ? row : this.heading()?.nativeElement)?.focus();
      },
      { injector: this.injector },
    );
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
