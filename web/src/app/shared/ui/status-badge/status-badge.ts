import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TaskStatus } from '../../../features/tasks/data/task.model';

const LABELS: Record<TaskStatus, string> = {
  ToDo: 'To do',
  InProgress: 'In progress',
  Done: 'Done',
};

@Component({
  selector: 'app-status-badge',
  template: `<span class="badge" [class]="'badge status-' + status().toLowerCase()">{{ label() }}</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
      background: var(--mat-sys-surface-container-highest);
      color: var(--mat-sys-on-surface-variant);
    }
    .status-inprogress {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    .status-done {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  readonly status = input.required<TaskStatus>();
  protected readonly label = computed(() => LABELS[this.status()]);
}
