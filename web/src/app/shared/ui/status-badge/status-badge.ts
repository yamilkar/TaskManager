import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { STATUS_LABELS, TaskStatus } from '../../../features/tasks/data/task.model';

@Component({
  selector: 'app-status-badge',
  template: `<span class="badge" [class]="'badge status-' + status().toLowerCase()">{{ label() }}</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 3px 10px;
      border-radius: 999px;
      font-size: 12px;
      font-weight: 600;
      white-space: nowrap;
      background: var(--tm-todo-badge-bg);
      color: var(--tm-todo-badge-fg);
    }
    .status-inprogress {
      background: var(--tm-prog-badge-bg);
      color: var(--tm-prog-badge-fg);
    }
    .status-done {
      background: var(--tm-done-badge-bg);
      color: var(--tm-done-badge-fg);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  readonly status = input.required<TaskStatus>();
  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
}
