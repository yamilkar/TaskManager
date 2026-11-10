import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '../../core/auth/auth-store';
import { Shell } from './shell';

describe('Shell', () => {
  it('Shell: shows user full name and "Sign out" logs out and navigates to /login', async () => {
    const logout = vi.fn();
    const user = signal({ id: '1', fullName: 'Ana Lopez', email: 'ana@x.com', createdAt: '' });
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthStore, useValue: { user, logout } }],
    });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.textContent).toContain('Ana Lopez');
    Array.from(el.querySelectorAll('button'))
      .find((b) => b.textContent?.includes('Sign out'))!
      .click();
    await fixture.whenStable();

    expect(logout).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
