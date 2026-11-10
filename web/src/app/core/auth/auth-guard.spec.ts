import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { authGuard } from './auth-guard';
import { AuthStore } from './auth-store';

describe('authGuard', () => {
  const isAuthenticated = signal(false);
  let router: Router;

  beforeEach(() => {
    isAuthenticated.set(false);
    TestBed.configureTestingModule({ providers: [{ provide: AuthStore, useValue: { isAuthenticated } }] });
    router = TestBed.inject(Router);
  });

  const run = () =>
    TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url: '/tasks?x=1' } as RouterStateSnapshot),
    );

  it('authGuard redirects unauthenticated users with returnUrl', () => {
    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Ftasks%3Fx%3D1');
  });

  it('allows authenticated users', () => {
    isAuthenticated.set(true);

    expect(run()).toBe(true);
  });
});
