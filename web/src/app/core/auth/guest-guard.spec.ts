import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { guestGuard } from './guest-guard';
import { AuthStore } from './auth-store';

describe('guestGuard', () => {
  const isAuthenticated = signal(false);
  let router: Router;

  beforeEach(() => {
    isAuthenticated.set(false);
    TestBed.configureTestingModule({ providers: [{ provide: AuthStore, useValue: { isAuthenticated } }] });
    router = TestBed.inject(Router);
  });

  const run = () =>
    TestBed.runInInjectionContext(() =>
      guestGuard({} as ActivatedRouteSnapshot, { url: '/login' } as RouterStateSnapshot),
    );

  it('guestGuard redirects authenticated users to /tasks', () => {
    isAuthenticated.set(true);

    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/tasks');
  });

  it('allows guests', () => {
    expect(run()).toBe(true);
  });
});
