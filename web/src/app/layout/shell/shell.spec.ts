import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '../../core/auth/auth-store';
import { Shell } from './shell';

describe('Shell', () => {
  function setup() {
    const logout = vi.fn();
    const user = signal({ id: '1', fullName: 'Ana Lopez', email: 'ana@x.com', createdAt: '' });
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthStore, useValue: { user, logout } }],
    });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, logout, navigate };
  }

  it('Shell: shows user full name and "Sign out" logs out and navigates to /login', async () => {
    const { fixture, el, logout, navigate } = setup();

    expect(el.textContent).toContain('Ana Lopez');
    Array.from(el.querySelectorAll('button'))
      .find((b) => b.textContent?.includes('Sign out'))!
      .click();
    await fixture.whenStable();

    expect(logout).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('Shell_signedIn_showsEmailAndInitials', () => {
    const { el } = setup();

    expect(el.textContent).toContain('ana@x.com');
    expect(el.querySelector('.avatar')!.textContent!.trim()).toBe('AL');
  });

  it('Shell_header_offersTheThemeSwitch', () => {
    const { el } = setup();

    expect(el.querySelector('header app-theme-toggle')).not.toBeNull();
  });
});
