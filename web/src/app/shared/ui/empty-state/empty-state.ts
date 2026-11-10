import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  template: `<p class="empty">{{ message() }}</p>`,
  styles: `
    .empty {
      margin: 0;
      padding: 32px 16px;
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmptyState {
  readonly message = input.required<string>();
}
