import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ThemeStore } from '../../../core/theme/theme-store';

/** Round sun/moon button that switches between the light and dark theme. */
@Component({
  selector: 'app-theme-toggle',
  template: `
    <button type="button" class="toggle" [attr.aria-label]="label()" [title]="label()" (click)="store.toggle()">
      @if (dark()) {
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          stroke-linecap="round" aria-hidden="true">
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
        </svg>
      } @else {
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z" />
        </svg>
      }
    </button>
  `,
  styles: `
    .toggle {
      width: 44px;
      height: 44px;
      border-radius: 50%;
      border: 1px solid var(--tm-hero-line);
      background: var(--tm-hero-chip);
      color: #fff;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }
    .toggle:hover {
      background: rgba(255, 255, 255, 0.22);
    }
    .toggle:focus-visible {
      outline: 2px solid #ffcc33;
      outline-offset: 2px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeToggle {
  protected readonly store = inject(ThemeStore);
  protected readonly dark = computed(() => this.store.theme() === 'dark');
  protected readonly label = computed(() => (this.dark() ? 'Switch to light theme' : 'Switch to dark theme'));
}
