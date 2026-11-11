import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The TaskManager mark (yellow tile with a check) and name. */
@Component({
  selector: 'app-brand-logo',
  template: `
    <svg width="32" height="32" viewBox="0 0 32 32" aria-hidden="true">
      <rect x="2" y="2" width="28" height="28" rx="8" fill="#ffcc33" />
      <path d="M10 16.5l4 4 8-9" fill="none" stroke="#2a1556" stroke-width="2.8" stroke-linecap="round"
        stroke-linejoin="round" />
    </svg>
    <span class="name">TaskManager</span>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      color: #ffffff;
    }
    .name {
      font-family: 'Space Grotesk', Inter, sans-serif;
      font-weight: 600;
      font-size: 19px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrandLogo {}
