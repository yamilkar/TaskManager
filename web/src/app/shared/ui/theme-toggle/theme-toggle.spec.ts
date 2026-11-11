import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ThemeStore } from '../../../core/theme/theme-store';
import { ThemeToggle } from './theme-toggle';

describe('ThemeToggle', () => {
  function setup(theme: 'light' | 'dark') {
    const toggle = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: ThemeStore, useValue: { theme: signal(theme), toggle } }],
    });
    const fixture = TestBed.createComponent(ThemeToggle);
    fixture.detectChanges();
    return { el: fixture.nativeElement as HTMLElement, toggle };
  }

  it('ThemeToggle_lightTheme_offersDarkTheme', () => {
    const { el } = setup('light');

    expect(el.querySelector('button')!.getAttribute('aria-label')).toBe('Switch to dark theme');
  });

  it('ThemeToggle_darkTheme_offersLightTheme', () => {
    const { el } = setup('dark');

    expect(el.querySelector('button')!.getAttribute('aria-label')).toBe('Switch to light theme');
  });

  it('ThemeToggle_click_togglesTheTheme', () => {
    const { el, toggle } = setup('light');

    el.querySelector('button')!.click();

    expect(toggle).toHaveBeenCalled();
  });
});
