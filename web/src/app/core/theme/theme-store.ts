import { Injectable, effect, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'tm.theme';

/**
 * The light or dark theme. The user's choice is remembered; without one the system preference decides.
 * The theme is applied as a `dark` class on `<html>`, which the global styles key their colours on.
 */
@Injectable({ providedIn: 'root' })
export class ThemeStore {
  private readonly themeState = signal<Theme>(initialTheme());

  readonly theme = this.themeState.asReadonly();

  constructor() {
    effect(() => {
      const theme = this.themeState();
      document.documentElement.classList.toggle('dark', theme === 'dark');
      try {
        localStorage.setItem(STORAGE_KEY, theme);
      } catch {
        // Storage unavailable: the choice lasts for this page only.
      }
    });
  }

  toggle(): void {
    this.themeState.update((theme) => (theme === 'dark' ? 'light' : 'dark'));
  }
}

function initialTheme(): Theme {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') {
      return stored;
    }
  } catch {
    // Storage unavailable: fall back to the system preference.
  }
  return typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}
