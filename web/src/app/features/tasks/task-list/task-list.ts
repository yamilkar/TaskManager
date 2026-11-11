import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { StatusBadge } from '../../../shared/ui/status-badge/status-badge';
import { STATUS_LABELS, TASK_STATUSES, Task, TaskStatus } from '../data/task.model';

@Component({
  selector: 'app-task-list',
  imports: [DatePipe, EmptyState, StatusBadge, MatButton, MatMenu, MatMenuItem, MatMenuTrigger],
  templateUrl: './task-list.html',
  styleUrl: './task-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskList {
  readonly tasks = input.required<Task[]>();
  /** The task open in the edit form; its Move menu is disabled so the form's status does not go stale. */
  readonly editingId = input<string | null>(null);
  readonly edit = output<Task>();
  readonly move = output<{ task: Task; status: TaskStatus }>();

  protected readonly statuses = TASK_STATUSES;
  protected readonly labels = STATUS_LABELS;
}
