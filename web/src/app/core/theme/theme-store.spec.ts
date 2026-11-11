import { TestBed } from '@angular/core/testing';
import { ThemeStore } from './theme-store';

const KEY = 'tm.theme';

describe('ThemeStore', () => {
  afterEach(() => {
    localStorage.removeItem(KEY);
    document.documentElement.classList.remove('dark');
    vi.unstubAllGlobals();
  });

  function create(prefersDark = false): ThemeStore {
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: prefersDark && query.includes('dark') }));
    const store = TestBed.inject(ThemeStore);
    TestBed.tick();
    return store;
  }

  it('ThemeStore_nothingStoredAndNoDarkPreference_isLight', () => {
    const store = create();

    expect(store.theme()).toBe('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('ThemeStore_nothingStoredAndSystemPrefersDark_isDark', () => {
    const store = create(true);

    expect(store.theme()).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('ThemeStore_storedChoice_winsOverSystemPreference', () => {
    localStorage.setItem(KEY, 'light');

    expect(create(true).theme()).toBe('light');
  });

  it('ThemeStore_invalidStoredValue_isIgnored', () => {
    localStorage.setItem(KEY, 'purple');

    expect(create().theme()).toBe('light');
  });

  it('ThemeStore_toggle_switchesAppliesAndRemembersTheTheme', () => {
    const store = create();

    store.toggle();
    TestBed.tick();

    expect(store.theme()).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem(KEY)).toBe('dark');

    store.toggle();
    TestBed.tick();

    expect(store.theme()).toBe('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
    expect(localStorage.getItem(KEY)).toBe('light');
  });
});
