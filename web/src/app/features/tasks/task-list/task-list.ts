import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { StatusBadge } from '../../../shared/ui/status-badge/status-badge';
import { Task } from '../data/task.model';

@Component({
  selector: 'app-task-list',
  imports: [DatePipe, StatusBadge],
  templateUrl: './task-list.html',
  styleUrl: './task-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskList {
  readonly tasks = input.required<Task[]>();
}
